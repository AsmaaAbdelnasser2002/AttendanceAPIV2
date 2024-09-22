using AttendanceAPIV2.Enums;
using AttendanceAPIV2.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using AttendanceAPIV2.Interfces;

namespace AttendanceAPIV2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
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



        private void UpdateOrCreateAttendanceRecord(string userId, DateTime? timeIn, DateTime? timeOut)
        {
            // Retrieve user from database
            var user = _context.Users.Find(userId);
            if (user == null)
            {
                // Optionally handle the case where the user does not exist
                // For example, log an error or throw an exception
                throw new ArgumentException("User not found.");
            }

            // Find existing attendance record
            var attendanceRecord = _context.AttendanceRecords
                .Where(ar => ar.UserId == userId && ar.TimeOut == null)
                .FirstOrDefault();

            if (attendanceRecord == null)
            {
                // Create a new attendance record if not found
                attendanceRecord = new AttendanceRecord
                {
                    UserId = userId,
                    TimeIn = timeIn ?? DateTime.Now,
                    Status = AttendanceStatus.Present
                };
                _context.AttendanceRecords.Add(attendanceRecord);
            }
            else
            {
                // Update existing attendance record
                if (timeIn.HasValue)
                {
                    attendanceRecord.TimeIn = timeIn.Value;
                }
                if (timeOut.HasValue)
                {
                    attendanceRecord.TimeOut = timeOut.Value;
                }

                // Optionally update status or other fields if needed
                attendanceRecord.Status = timeOut.HasValue ? AttendanceStatus.Present : attendanceRecord.Status;
            }

            _context.SaveChanges();
        }

        [HttpPost("UploadExcel")]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var filePath = Path.Combine(Path.GetTempPath(), file.FileName);

            // Save the uploaded file to a temporary path
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                // Read the Excel file
                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    var worksheet = package.Workbook.Worksheets[0];

                    // Check if the worksheet has data
                    if (worksheet.Dimension == null)
                    {
                        return BadRequest("The uploaded Excel file is empty or does not have a valid format.");
                    }

                    var rowCount = worksheet.Dimension.Rows;

                    for (int row = 2; row <= rowCount; row++) // Assuming row 1 contains headers
                    {
                        var userId = worksheet.Cells[row, 1].Text;
                        var name = worksheet.Cells[row, 2].Text;
                        var sessionId = int.TryParse(worksheet.Cells[row, 3].Text, out int sId) ? sId : (int?)null;
                        var status = worksheet.Cells[row, 4].Text;
                        var timeIn = DateTime.TryParse(worksheet.Cells[row, 5].Text, out DateTime tIn) ? tIn : (DateTime?)null;
                        var timeOut = DateTime.TryParse(worksheet.Cells[row, 6].Text, out DateTime tOut) ? tOut : (DateTime?)null;

                        // Ensure the sessionId is valid and userId exists
                        var sessionExists = await _context.Sessions.AnyAsync(s => s.SessionId == sessionId);
                        if (!sessionExists)
                        {
                            // Handle invalid sessionId case
                            continue;
                        }

                        // Update or create the attendance record
                        UpdateOrCreateAttendanceRecord(userId, timeIn, timeOut);
                    }

                    return Ok(new { message = "Excel file processed successfully." });
                }
            }
            catch (Exception ex)
            {
                // Log the exception and return a server error
                // You might want to use a logging library or system here
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while processing the file.", error = ex.Message });
            }
        }
        private string GetMostRecentExcelFile()
        {
            // Define the directory where the Excel files are stored
            var files = Directory.GetFiles(_uploadDirectory, "*.xlsx");

            // If no files are found, return null or handle as appropriate
            if (files.Length == 0)
            {
                return null;
            }

            // Order files by creation time in descending order and take the first one (most recent)
            return files.OrderByDescending(f => new FileInfo(f).CreationTime).FirstOrDefault();
        }
        private void UpdateExcelFile(string userId, DateTime? timeIn, DateTime? timeOut)
        {
            var mostRecentFile = GetMostRecentExcelFile();
            if (mostRecentFile == null)
            {
                throw new FileNotFoundException("No Excel file found.");
            }

            using (var package = new ExcelPackage(new FileInfo(mostRecentFile)))
            {
                var worksheet = package.Workbook.Worksheets[0];
                var rowCount = worksheet.Dimension.Rows;

                for (int row = 2; row <= rowCount; row++)
                {
                    var cellUserId = worksheet.Cells[row, 1].Text;

                    if (cellUserId == userId)
                    {
                        worksheet.Cells[row, 2].Value = "Present";
                        worksheet.Cells[row, 3].Value = timeIn?.ToString("o");
                        worksheet.Cells[row, 4].Value = timeOut?.ToString("o");
                        package.Save();
                        return;
                    }
                }
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

            // Check if the user has already checked in
            var existingAttendanceRecord = await _context.AttendanceRecords
                .Where(ar => ar.SessionId == checkInRequest.SessionId && ar.UserId == checkInRequest.UserId)
                .FirstOrDefaultAsync();

            if (existingAttendanceRecord != null)
            {
                return BadRequest("User has already checked in for this session.");
            }

            // Mark the user as "Present"
            var attendanceRecord = new AttendanceRecord
            {
                UserId = checkInRequest.UserId,
                SessionId = checkInRequest.SessionId,
                TimeIn = DateTime.Now,
                Status = AttendanceStatus.Present
            };

            _context.AttendanceRecords.Add(attendanceRecord);
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

            if (attendanceRecord == null)
            {
                // If the user has not checked in, mark them as checked in
                attendanceRecord = new AttendanceRecord
                {
                    UserId = checkInRequest.UserId,
                    SessionId = checkInRequest.SessionId,
                    TimeIn = DateTime.Now,
                    Status = AttendanceStatus.Present
                };
                _context.AttendanceRecords.Add(attendanceRecord);
            }
            else if (attendanceRecord.TimeOut == null)
            {
                // If the user is already checked in but hasn't checked out, mark them as checked out
                attendanceRecord.TimeOut = DateTime.Now;
            }
            else
            {
                return BadRequest("User has already checked out.");
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Attendance recorded successfully." });
        }

        // Mark attendance for a student in a specific session
        [HttpPost("MarkAttendance/sessionId")]
        public async Task<ActionResult> MarkAttendance(int sessionId, string userId)
        {
            // Check if the session exists
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null)
            {
                return NotFound("Session not found.");
            }

            // Check if the student exists
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            // Create a new attendance record
            var attendanceRecord = new AttendanceRecord
            {
                SessionId = sessionId,
                UserId = userId,
                TimeIn = DateTime.Now,
                Status = AttendanceStatus.Present,  // Assuming you're marking them as 'Present' for now
                TimeOut = null
            };

            _context.AttendanceRecords.Add(attendanceRecord);
            await _context.SaveChangesAsync();

            return Ok("Attendance marked successfully.");
        }

        // Update TimeOut for a specific student's attendance in a session
        [HttpPut("UpdateTimeOutForAUser")]
        public async Task<ActionResult> UpdateTimeOutForAUser(int sessionId, string userId)
        {
            // Find the attendance record for the specific session and student
            var attendanceRecord = await _context.AttendanceRecords
                .FirstOrDefaultAsync(ar => ar.SessionId == sessionId && ar.UserId == userId);

            if (attendanceRecord == null)
            {
                return NotFound("Attendance record not found.");
            }

            if (attendanceRecord.Status != AttendanceStatus.Present)
            {
                return BadRequest("Cannot update TimeOut for a User marked as absent.");
            }

            // Check if TimeOut is already set
            if (attendanceRecord.TimeOut != null)
            {
                return BadRequest("TimeOut is already set for this User.");
            }

            // Update the TimeOut to the current time
            attendanceRecord.TimeOut = DateTime.Now;

            // Save changes to the database
            _context.AttendanceRecords.Update(attendanceRecord);
            await _context.SaveChangesAsync();

            return Ok("TimeOut updated successfully.");
        }

        // Search for students based on a keyword
        [HttpGet("SearchForUser")]
        public async Task<ActionResult<IEnumerable<User>>> SearchForUser([FromQuery] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return BadRequest("Keyword is required.");
            }

            var users = await _context.Users
                .Where(u => u.UserName.Contains(keyword) || u.Id.Contains(keyword))
                .ToListAsync();

            if (!users.Any())
            {
                return NotFound("No students found.");
            }

            return Ok(users);
        }
    }
}