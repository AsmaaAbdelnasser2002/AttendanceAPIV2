using AttendanceAPIV2.Enums;
using AttendanceAPIV2.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using AttendanceAPIV2.Interfces;
using Microsoft.AspNetCore.Authorization;
using ClosedXML.Excel;
using System.Text;

namespace AttendanceAPIV2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AttendanceController : ControllerBase
    {
        private readonly AttendanceContext _context;
        private readonly QRCodeService _qrCodeService;
        //private readonly IFaceRecognitionService _faceRecognitionService;
        private readonly string _uploadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "UploadedFiles");
        private readonly string _userImagesFolder = Path.Combine(Directory.GetCurrentDirectory(), "FacesFolder");

        public AttendanceController(AttendanceContext context, QRCodeService qrCodeService)
        {
            _context = context;
            _qrCodeService = qrCodeService;
            //_faceRecognitionService = faceRecognitionService;
        }


        

        [HttpGet("GenerateQRCode")]
        public async Task<IActionResult> GenerateQRCode([FromQuery] int? sessionId, [FromQuery] int? examId)
        //public async Task<IActionResult> GenerateQRCode(int sessionId)
        {
            if (sessionId == null && examId == null)
            {
                return BadRequest("sessionId is required.");
            }
            int id;
            if (examId != null)
            {
                var sessionId2 = await _context.Sessions.Where(i => i.ExamId == examId).Select(i => i.SessionId).FirstOrDefaultAsync();
                id = sessionId2;
            }
            else
            {
                id = sessionId.Value;
            }
            var session = await _context.Sessions.FindAsync(id);
            if (session == null) return NotFound("Session not found.");

            string qrCodeData = $"{sessionId}-{Guid.NewGuid()}";
            var qrCode = _qrCodeService.GenerateQRCode(qrCodeData);

            var sessionQRCode = new SessionQrCode
            {
                SessionId = id,
                Code = qrCode,
                GeneratedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddMinutes(5)
            };
            _context.SessionQRCodes.Add(sessionQRCode);
            await _context.SaveChangesAsync();

            return Ok(sessionQRCode);
        }

        [HttpPost("CheckIn")]
        public async Task<IActionResult> CheckIn([FromBody] CheckInRequest checkInRequest)
        {

            // Validate the session QR code
            var sessionQRCode = await _context.SessionQRCodes
                .Where(q => q.SessionId == checkInRequest.SessionId && q.Code == checkInRequest.QRCodeData && q.ExpiresAt >= DateTime.Now)
                .FirstOrDefaultAsync();

            if (sessionQRCode == null)
            {
                return BadRequest("Invalid or expired QR code.");
            }

            // Validate the user
            var user = await _context.Users.Where(i => i.ExaminerId == checkInRequest.UserId).Select(i => i.Id).FirstOrDefaultAsync();
            //var user = await _context.Users.FindAsync(checkInRequest.UserId);
            if (user == null)
            {
                return BadRequest("User not found.");
            }

            var attendanceRecord = await _context.AttendanceRecords.FirstOrDefaultAsync(x => x.UserId == user && x.SessionId == checkInRequest.SessionId);
            if (attendanceRecord == null)
            {
                return NotFound("Attendance record not found for the specified user and session.");
            }
            var session = await _context.Sessions.FindAsync(checkInRequest.SessionId);
            if (session.TimeLimit >= DateTime.Now)
            {
                // Modify the properties as needed
                attendanceRecord.TimeIn = DateTime.Now; // Update time in if needed
                attendanceRecord.Status = AttendanceStatus.Present; // Set the desired status                                      
                                                                    // Save the changes to the database
                await _context.SaveChangesAsync();

                var message = new StringBuilder();
                message.AppendLine($"You presented session: {session.SessionName}");

                var notification = new Notification
                {
                    UserId = user,
                    Message = message.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();


                return Ok(new { message = "Attendance recorded successfully." });
            }
            else 
            {
                return Ok(new { message = "You passed the time limit." });
            }
        }

        [HttpPost("CheckInOut")]
        public async Task<IActionResult> CheckInOut([FromBody] CheckInRequest checkInRequest)
        {

            // Validate the session QR code
            var sessionQRCode = await _context.SessionQRCodes
                .Where(q => q.SessionId == checkInRequest.SessionId && q.Code == checkInRequest.QRCodeData && q.ExpiresAt >= DateTime.Now)
                .FirstOrDefaultAsync();

            if (sessionQRCode == null)
            {
                return BadRequest("Invalid or expired QR code.");
            }

            // Validate the user
            var user = await _context.Users.FindAsync(checkInRequest.UserId);
            if (user == null)
            {
                return BadRequest("User not found.");
            }

            // Check if the user has already checked in
            var attendanceRecord = await _context.AttendanceRecords
                .Where(ar => ar.SessionId == checkInRequest.SessionId && ar.UserId == checkInRequest.UserId)
                .FirstOrDefaultAsync();
             if (attendanceRecord == null && attendanceRecord.TimeOut == null)
            {
                // If the user is already checked in but hasn't checked out, mark them as checked out
                attendanceRecord.TimeOut = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Attendance recorded successfully." });
        }

        [HttpPost("MarkAttendance/sessionId")]
        public async Task<ActionResult> MarkAttendance(int sessionId, string username)
        {
            // Check if the session exists
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null)
            {
                return NotFound("Session not found.");
            }

            byte[] excelData = session.Sheet;
            using (var stream = new MemoryStream(excelData))
            {
                using (var workbook = new XLWorkbook(stream))
                {
                    var worksheet = workbook.Worksheets.First();
                    List<(string UserId, string Username)> matchingUsers = new List<(string, string)>();

                    foreach (var row in worksheet.RowsUsed())
                    {
                        var sheetUsername = row.Cell(2).GetString(); // Adjust column index for username
                        if (sheetUsername == username)
                        {
                            var userId = row.Cell(1).GetString(); // Adjust column index for UserId
                            matchingUsers.Add((userId, sheetUsername));
                        }
                    }

                    if (matchingUsers.Count == 0)
                    {
                        return NotFound("Username not found in the session's Excel sheet.");
                    }
                    else if (matchingUsers.Count == 1)
                    {
                        // Only one match, proceed with marking attendance
                        var userId = matchingUsers.First().UserId;

                        var attendanceRecord = await _context.AttendanceRecords
                            .FirstOrDefaultAsync(x => x.UserId == userId && x.SessionId == sessionId);

                        if (attendanceRecord == null)
                        {
                            return NotFound("Attendance record not found for the specified user and session.");
                        }

                        // Modify the properties as needed
                        attendanceRecord.TimeIn = DateTime.Now; // Update time in if needed
                        attendanceRecord.Status = AttendanceStatus.Present; // Set the desired status                                      
                                                                            // Save the changes to the database
                        await _context.SaveChangesAsync();


                        var message = new StringBuilder();
                        message.AppendLine($"You presented session: {session.SessionName}");

                        var notification = new Notification
                        {
                            UserId = userId,
                            Message = message.ToString(),
                            CreatedAt = DateTime.UtcNow,
                            IsRead = false
                        };

                        _context.Notifications.Add(notification);
                        await _context.SaveChangesAsync();

                        return Ok("Attendance marked successfully.");
                    }
                    else
                    {
                        // Multiple matches found, return the list of potential matches
                        return Ok(new
                        {
                            message = "Multiple users found with the same username.",
                            potentialMatches = matchingUsers
                        });
                    }
                }
            }
        }

        [HttpGet("Exam_Attendance")]
        public async Task<IActionResult> Exam_Attendance([FromQuery] int examId)
        {
            // Step 1: Find the session ID associated with the given examId
            var sessionId = await _context.Sessions
                .Where(s => s.ExamId == examId)
                .Select(s => s.SessionId)
                .FirstOrDefaultAsync();

            if (sessionId == 0) // Check if the session exists
            {
                return NotFound("No session found for the provided examId.");
            }

            // Step 2: Retrieve all attendance records for the found session
            var attendanceRecords = await _context.AttendanceRecords
                .Where(ar => ar.SessionId == sessionId)
                .ToListAsync();

            // Step 3: Create a lookup dictionary for attendance status by UserId
            var attendanceStatus = attendanceRecords.ToDictionary(ar => ar.UserId, ar => ar.Status);

            // Step 4: Get the examiner ID and attendance status for each user
            var examiners = await _context.Users
                .Where(u => attendanceStatus.Keys.Contains(u.Id))
                .Select(u => new
                {
                    u.ExaminerId,
                    AttendanceStatus = attendanceStatus[u.Id] == 0 ? "Present" : "Absent" // Change to "Present" or "Absent"
                })
                .ToListAsync();

            // Step 5: Return the list of examiners with their corresponding attendance status
            return Ok(examiners);
        }

    }
}