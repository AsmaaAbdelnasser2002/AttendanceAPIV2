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


        [HttpPost("AddFromSession/{sessionId}")]
        public async Task<IActionResult> AddAttendanceRecordsFromSession(int sessionId)
        {
            try
            {
                // Fetch the session from the database
                var session = _context.Sessions.Find(sessionId);
                if (session == null || session.Sheet == null)
                {
                    return NotFound("Session not found or sheet is empty.");
                }

                // Read the Excel file from the binary data
                using (var stream = new MemoryStream(session.Sheet))
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets.FirstOrDefault(); // Get the first worksheet or null

                    if (worksheet == null || worksheet.Dimension == null)
                    {
                        return BadRequest("The Excel sheet is empty or does not contain any data.");
                    }

                    var attendanceRecords = new List<AttendanceRecord>();

                    // Check the number of rows and start processing
                    for (int row = 2; row <= worksheet.Dimension.End.Row; row++) // Start from row 2 if row 1 is headers
                    {
                        var studentId = worksheet.Cells[row, 1].Text; // Assuming ID is in column 1
                        var us=await _context.Users.FindAsync(studentId);

                        if (!string.IsNullOrEmpty(studentId) && us!=null)
                        {
                            attendanceRecords.Add(new AttendanceRecord
                            {
                                TimeIn = DateTime.Now, // Set the current time
                                Status = AttendanceStatus.Absent,
                                UserId = studentId,
                                SessionId = sessionId
                            });
                        }
                    }

                    // Check if any attendance records were created
                    if (attendanceRecords.Count == 0)
                    {
                        return BadRequest("No valid student IDs found in the Excel sheet.");
                    }

                    // Add records to the database
                    _context.AttendanceRecords.AddRange(attendanceRecords);
                    _context.SaveChanges();
                }

                return Ok("Attendance records added successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
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

            // Modify the properties as needed
            attendanceRecord.TimeIn = DateTime.Now; // Update time in if needed
            attendanceRecord.Status = AttendanceStatus.Present; // Set the desired status                                      
            // Save the changes to the database
            await _context.SaveChangesAsync();

            return Ok(new { message = "Attendance recorded successfully." });
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