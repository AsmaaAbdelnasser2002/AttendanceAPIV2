using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AttendanceAPIV2.Models;
using AttendanceAPIV2.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using AttendanceAPIV2.Enums;
using System.Net.Http;
using OfficeOpenXml;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace AttendanceAPIV2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SessionController : ControllerBase
    {
        private readonly AttendanceContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly UserManager<User> _userManager;

        public SessionController(UserManager<User> userManager, IHttpClientFactory httpClientFactory, AttendanceContext context)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _userManager = userManager;
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
                        var us = await _context.Users.FindAsync(studentId);

                        if (!string.IsNullOrEmpty(studentId) && us != null)
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
      
        [HttpPost("ProcessExcelAndCreateUsers/{sessionId}")]
        public async Task<IActionResult> ProcessExcelAndCreateUsers(int sessionId)
        {
            // Retrieve the session
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null || session.Sheet == null)
            {
                return NotFound("Session or Excel data not found.");
            }

            // Load the binary Excel data from the session
            using var memoryStream = new MemoryStream(session.Sheet);
            using var package = new ExcelPackage(memoryStream);

            // Get the first worksheet in the Excel file
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                return BadRequest("No worksheet found in the Excel file.");
            }

            // Iterate through the rows of the worksheet (starting at row 2 to skip headers)
            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
            {
                var existingId = worksheet.Cells[row, 1].Text;

                if (string.IsNullOrWhiteSpace(existingId))
                {
                    continue; // Skip if no ID
                }

                // Parse the age from column 5 (E)
                int age;
                if (!int.TryParse(worksheet.Cells[row, 5].Text, out age))
                {
                    age = 18; // Default age
                }

                // Check if the user already exists based on the ExaminerId
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.ExaminerId == existingId);

                if (existingUser == null)
                {
                    // Create a new User object
                    var newUser = new User
                    {
                        UserName = worksheet.Cells[row, 2].Text,
                        Email = worksheet.Cells[row, 3].Text,
                        Age = age,
                        Gender = worksheet.Cells[row, 6].Text,
                        UserRole = "Attender",
                        ExaminerId = existingId // This will be used to identify the user
                    };

                    // Attempt to create the user
                    IdentityResult result = await _userManager.CreateAsync(newUser, worksheet.Cells[row, 4].Text); // Password from column 4
                    if (result.Succeeded)
                    {
                        // The user is already added to the Users table, save changes
                        await _context.SaveChangesAsync();

                        // Replace the existing ID in the sheet with the newly generated UserId
                        worksheet.Cells[row, 1].Value = newUser.Id; // Store the new ID in the Excel sheet
                    }
                    else
                    {
                        // Log or handle the error accordingly
                        return BadRequest("Error creating user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    // If the user already exists, use the existing user ID
                    worksheet.Cells[row, 1].Value = existingUser.Id; // Replace with the existing user's ID
                }
            }

            // Save the modified Excel file to a new memory stream
            var updatedExcelStream = new MemoryStream();
            package.SaveAs(updatedExcelStream);

            session.Sheet = updatedExcelStream.ToArray();

            _context.Sessions.Update(session);
            await _context.SaveChangesAsync();

            return Ok();
        }


        [HttpPost("create")]
        public async Task<IActionResult> createSession([FromForm] SessionDto sessionDto, [FromQuery] int? foldreId, [FromQuery] int? examId)
        {

            if (examId == null)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null)
                {
                    return BadRequest(new { message = "Please login First." });
                }
                var user = _context.Users.FirstOrDefault(u => u.Id == userId && u.UserRole == "Instructor");
                if (user == null)
                {
                    return BadRequest(new { message = "You are not authorized, please login with an Instructor account." });
                }

            }
            else
            {
                var id = await _context.Sessions.Where(i => i.ExamId == examId).FirstOrDefaultAsync();
                if (id != null)
                {
                    return BadRequest(new { message = "This exam session is already exist." });
                }
            }
            // Create and save the session
            var session = new Session
            {
                SessionName = sessionDto.SessionName,
                SessionDescription = sessionDto.SessionDescription,
                StartTime = sessionDto.StartTime,
                EndTime = sessionDto.EndTime,
                SessionPlace = sessionDto.SessionPlace,
                TimeLimit = sessionDto.TimeLimit
            };
            session.ExamId = examId;
            
            if (foldreId != null)
            {
                var folder = await _context.Folders.FindAsync(foldreId);
                if (sessionDto.FacesFolder != null && folder.FacesFolder == null)
                {
                    using var stream1 = new MemoryStream();
                    await sessionDto.FacesFolder.CopyToAsync(stream1);
                    session.FacesFolder = stream1.ToArray();
                }
                else
                {
                    session.FacesFolder = folder.FacesFolder;
                }
                if (sessionDto.VoicesFolder != null && folder.VoicesFolder == null)
                {
                    using var stream2 = new MemoryStream();
                    await sessionDto.VoicesFolder.CopyToAsync(stream2);
                    session.VoicesFolder = stream2.ToArray();
                }
                else
                {
                    session.VoicesFolder = folder.VoicesFolder;

                }
                session.Sheet = folder.Sheet;
              
            }
            else
            {
                if (sessionDto.FacesFolder != null)
                {
                    using var stream1 = new MemoryStream();
                    await sessionDto.FacesFolder.CopyToAsync(stream1);
                    session.FacesFolder = stream1.ToArray();
                }
                if (sessionDto.VoicesFolder != null)
                {
                    using var stream2 = new MemoryStream();
                    await sessionDto.VoicesFolder.CopyToAsync(stream2);
                    session.VoicesFolder = stream2.ToArray();
                }
                if (sessionDto.Sheet != null)
                {
                    using var stream3 = new MemoryStream();
                    await sessionDto.Sheet.CopyToAsync(stream3);
                    session.Sheet = stream3.ToArray();
                }
                else
                {
                    return BadRequest(new { message = "upload excel sheet,please." });
                }
                if (sessionDto.FacesFolder == null && sessionDto.VoicesFolder == null && sessionDto.Sheet == null)
                {
                    return BadRequest(new { message = "upload one file at least." });
                }
            }

            if (examId == null)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                session.User_Id = userId;
            }
            session.Folder_Id = foldreId;
            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();

           
            var re1 = await ProcessExcelAndCreateUsers(session.SessionId);

            // Read Excel File and save it in record
            var re = await AddAttendanceRecordsFromSession(session.SessionId);
            if (re != null)
            {
                // Add a notification for a specific user
                List<AttendanceRecord> attendanceRecords = _context.AttendanceRecords.
                    Where(x => x.SessionId == session.SessionId).ToList();

                // Create the message string
                var message = new StringBuilder();
                message.AppendLine($"You are added to this session");
                message.AppendLine($"Session Name: {session.SessionName}");
                message.AppendLine($"Session Place: {session.SessionPlace}");
                message.AppendLine($"Session Description: {session.SessionDescription}");
                message.AppendLine($"Start Time: {session.StartTime}");
                message.AppendLine($"End Time: {session.EndTime}");
                message.AppendLine($"Time Limit: {session.TimeLimit}");
                if (session.Folder_Id != null)
                { message.AppendLine($"Folder Path: {session.Folder.FolderPath}"); }
                foreach (var item in attendanceRecords)
                {
                    if (item != null)
                    {
                        var notification = new Notification
                        {
                            UserId = item.UserId,
                            Message = message.ToString(),
                            CreatedAt = DateTime.UtcNow,
                            IsRead = false
                        };

                        _context.Notifications.Add(notification);
                        await _context.SaveChangesAsync();
                    }
                }
            }

            return Ok(new { id = session.SessionId });
        }


        [HttpGet("All_Sessions")]
        public async Task<ActionResult<List<SessionListDto>>> GetSessions()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return BadRequest(new { message = "Please login First." });
            }

            var sessionSummaries = await _context.Sessions
                .Where(s => s.Folder_Id==null)
                .Select(p => new SessionListDto
                {
                    SessionId=p.SessionId,
                    SessionName = p.SessionName,
                    StartTime = p.StartTime,
                    EndTime = p.EndTime,
                    creator = p.User.UserName
                })
                .ToListAsync();

            return Ok(sessionSummaries);
            
        }

        //[HttpGet("userSessions")]
        //public async Task<IActionResult> GetUserSessions()
        //{
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    if (userId == null)
        //    {
        //        return BadRequest(new { message = "Please login First." });
        //    }

           
        //    var sessions = await _context.Sessions
        //        .Where(p => p.User_Id == userId)
        //        .Select(p => new { p.SessionName })
        //        .ToListAsync();

        //    return Ok(sessions);
        //}

        [HttpGet("Session_Data/{id}")]
        public async Task<ActionResult<List<SessionDataDto>>> DataOfSession(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return BadRequest(new { message = "Please login First." });
            }

            var data = await _context.Sessions
                .Where(p => p.SessionId == id)
                .Select(p => new SessionDataDto
                {
                    SessionId= p.SessionId,
                    SessionName = p.SessionName,
                    SessionPlace = p.SessionPlace,
                    SessionDescription = p.SessionDescription,
                    StartTime = p.StartTime,
                    EndTime = p.EndTime,
                    TimeLimit = p.TimeLimit,
                    creator = p.User.UserName,
                    ExcelSheetUrl = Url.Action(nameof(GetExcelSheet), new { id }),
                    FacesFolderUrl = Url.Action(nameof(GetFacesFolder), new { id }),
                    VoicesFolderUrl = Url.Action(nameof(GetVoicesFolder), new { id })
                })
                .FirstOrDefaultAsync();

            return Ok(data);
        }

        [HttpPut("Edit_Session/{id}")]
        public async Task<IActionResult> EditSession(int id, [FromForm] EditSessionDto editSessionDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return BadRequest(new { message = "Please login First." });
            }

            var user = _context.Users.FirstOrDefault(u => u.Id == userId && u.UserRole == "Instructor");
            if (user == null)
            {
                return BadRequest(new { message = "You are not authorized, please login with an Instructor account." });
            }


            var session = await _context.Sessions.FindAsync(id);

            if (session == null)
            {
                return NotFound();
            }

            if (session.User_Id != userId)
            {
                return Unauthorized();
            }
            if (editSessionDto.SessionName != null)
            { session.SessionName = editSessionDto.SessionName; }
            if (editSessionDto.SessionDescription != null)
            { session.SessionDescription = editSessionDto.SessionDescription; }
            if (editSessionDto.StartTime != null)
            { session.StartTime = editSessionDto.StartTime; }
            if (editSessionDto.EndTime != null)
            { session.EndTime = editSessionDto.EndTime; }
            if(editSessionDto.TimeLimit!=null)
            { session.TimeLimit = editSessionDto.TimeLimit; }
            if (editSessionDto.FacesFolder != null && session.Folder_Id == null)
            {
                using var stream1 = new MemoryStream();
                await editSessionDto.FacesFolder.CopyToAsync(stream1);
                session.FacesFolder = stream1.ToArray();
            }
            if (editSessionDto.VoicesFolder != null && session.Folder_Id == null)
            {
                using var stream2 = new MemoryStream();
                await editSessionDto.VoicesFolder.CopyToAsync(stream2);
                session.VoicesFolder = stream2.ToArray();
            }
            if (editSessionDto.Sheet != null && session.Folder_Id == null)
            {
                using var stream3 = new MemoryStream();
                await editSessionDto.Sheet.CopyToAsync(stream3);
                session.Sheet = stream3.ToArray();
            }
            _context.Sessions.Update(session);
            await _context.SaveChangesAsync();      
            return Ok(new { message = "session updated successfully." });
        }

        [HttpDelete("Delete_Session/{id}")]
        public async Task<IActionResult> DeleteSession(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return BadRequest(new { message = "Please login First." });
            }

            var user = _context.Users.FirstOrDefault(u => u.Id == userId && u.UserRole == "Instructor");
            if (user == null)
            {
                return BadRequest(new { message = "You are not authorized, please login with an Instructor account." });
            }

            var session = await _context.Sessions.FindAsync(id);

            if (session == null)
            {
                return NotFound();
            }

            if (session.User_Id != userId)
            {
                return Unauthorized();
            }

            var recordList = await _context.AttendanceRecords
                 .Where(r => r.SessionId == session.SessionId)
                 .ToListAsync();
            _context.AttendanceRecords.RemoveRange(recordList);
            await _context.SaveChangesAsync();
            _context.Sessions.Remove(session);
            await _context.SaveChangesAsync();
            return Ok(new { message = "session deleted successfully." });
        }


        [HttpGet("download/sheet/{id}")]
        public async Task<IActionResult> GetExcelSheet(int id)
        {
            var session = await _context.Sessions.FindAsync(id);
            if (session == null)
            {
                return NotFound();
            }

            if (session.Sheet == null)
            {
                return NotFound("Excel sheet not found.");
            }

            return File(session.Sheet, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "sheet.xlsx");
        }

        [HttpGet("download/facesfolder/{id}")]
        public async Task<IActionResult> GetFacesFolder(int id)
        {
            var session = await _context.Sessions.FindAsync(id);
            if (session == null)
            {
                return NotFound();
            }

            if (session.FacesFolder == null)
            {
                return NotFound("Faces folder file not found.");
            }

            return File(session.FacesFolder, "application/x-rar-compressed", "facesfolder.rar");
        }

        [HttpGet("download/voicesfolder/{id}")]
        public async Task<IActionResult> GetVoicesFolder(int id)
        {
            var session = await _context.Sessions.FindAsync(id);
            if (session == null)
            {
                return NotFound();
            }

            if (session.VoicesFolder == null)
            {
                return NotFound("Voices folder file not found.");
            }

            return File(session.VoicesFolder, "application/x-rar-compressed", "voicesfolder.rar");
        }

        [HttpGet("SessionReport/{sessionId}")]
        public async Task<IActionResult> GetSessionReport(int sessionId)
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null)
            {
                return NotFound();
            }
            var attendanceRecords = _context.AttendanceRecords
                .Where(ar => ar.SessionId == sessionId)
                .Include(ar => ar.Session)
                .Include(ar => ar.User)
                .ToList();

            var attendedOnTime = attendanceRecords
                .Where(ar => ar.Status == AttendanceStatus.Present && ar.TimeIn <= ar.Session.StartTime)
                .Select(ar => ar.User.UserName)
                .ToList();

            var lateAttendees = attendanceRecords
                .Where(ar => ar.Status == AttendanceStatus.Present && ar.TimeIn > ar.Session.StartTime)
                .Select(ar => ar.User.UserName)
                .ToList();

            var absentAttendees = attendanceRecords
                .Where(ar => ar.Status == AttendanceStatus.Absent)
                .Select(ar => ar.User.UserName)
                .ToList();
            return Ok(new
            {
                AttendedOnTime = attendedOnTime,
                LateAttendees = lateAttendees,
                AbsentAttendees = absentAttendees
            });

        }

        [HttpGet("SessionReportExcel/{sessionId}")]
        public async Task<IActionResult> GetSessionReportExcel(int sessionId)
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null)
            {
                return NotFound();
            }

            var attendanceRecords = _context.AttendanceRecords
                .Where(ar => ar.SessionId == sessionId)
                .Include(ar => ar.Session)
                .Include(ar => ar.User)
                .ToList();

            var attendedOnTime = attendanceRecords
                .Where(ar => ar.Status == AttendanceStatus.Present && ar.TimeIn <= ar.Session.StartTime)
                .Select(ar => ar.User.UserName)
                .ToList();

            var lateAttendees = attendanceRecords
                .Where(ar => ar.Status == AttendanceStatus.Present && ar.TimeIn > ar.Session.StartTime)
                .Select(ar => ar.User.UserName)
                .ToList();

            var absentAttendees = attendanceRecords
                .Where(ar => ar.Status == AttendanceStatus.Absent)
                .Select(ar => ar.User.UserName)
                .ToList();

            // Create a new Excel package
            using (var package = new ExcelPackage())
            {
                // Add a new worksheet
                var worksheet = package.Workbook.Worksheets.Add("Attendance Report");

                // Add header row
                worksheet.Cells[1, 1].Value = "Status";
                worksheet.Cells[1, 2].Value = "User Name";

                // Add attended on time data
                int row = 2;
                foreach (var username in attendedOnTime)
                {
                    worksheet.Cells[row, 1].Value = "On Time";
                    worksheet.Cells[row, 2].Value = username;
                    row++;
                }

                // Add late attendees data
                foreach (var username in lateAttendees)
                {
                    worksheet.Cells[row, 1].Value = "Late";
                    worksheet.Cells[row, 2].Value = username;
                    row++;
                }

                // Add absent attendees data
                foreach (var username in absentAttendees)
                {
                    worksheet.Cells[row, 1].Value = "Absent";
                    worksheet.Cells[row, 2].Value = username;
                    row++;
                }

                // Set the content type and file name
                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "SessionReport.xlsx");
            }
        }

    }
    
}
