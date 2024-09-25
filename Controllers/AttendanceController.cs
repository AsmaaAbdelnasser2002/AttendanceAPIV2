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


        

        [HttpGet("GenerateQRCode/{sessionId}")]
        public async Task<IActionResult> GenerateQRCode(int sessionId)
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null) return NotFound("Session not found.");

            string qrCodeData = $"{sessionId}-{Guid.NewGuid()}";
            var qrCode = _qrCodeService.GenerateQRCode(qrCodeData);

            var sessionQRCode = new SessionQrCode
            {
                SessionId = sessionId,
                Code = qrCode,
                GeneratedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddSeconds(30)
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
            var user = await _context.Users.FindAsync(checkInRequest.UserId);
            if (user == null)
            {
                return BadRequest("User not found.");
            }

            var attendanceRecord = await _context.AttendanceRecords.FirstOrDefaultAsync(x => x.UserId == checkInRequest.UserId && x.SessionId == checkInRequest.SessionId);
            if (attendanceRecord == null)
            {
                return NotFound("Attendance record not found for the specified user and session.");
            }
            var session = await _context.Sessions.FindAsync(checkInRequest.SessionId);
            if (session.TimeLimit <= DateTime.Now)
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
                    UserId = checkInRequest.UserId,
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

       
    }
}