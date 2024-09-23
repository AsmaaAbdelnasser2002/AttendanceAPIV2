using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AttendanceAPIV2.Models;
using AttendanceAPIV2.Models.DTOs;
using Microsoft.IdentityModel.Tokens;
using static NuGet.Packaging.PackagingConstants;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OfficeOpenXml.Style;
using System.Data;

namespace AttendanceAPIV2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FoldersController : ControllerBase
    {
        private readonly AttendanceContext _context;
        public FoldersController(AttendanceContext context)
        {
            _context = context;
        }

        // GET: api/Folders
        [HttpGet("GetFolders")]
        public async Task<ActionResult<IEnumerable<Folder>>> GetFolders()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return BadRequest(new { message = "Please login First." });
            }

            var folders = await _context.Folders
                .Where(p => p.User_Id == userId && p.ParentFolder==null)
                .ToListAsync();

            return Ok(folders);
        }

        // GET: api/Folders/5
        [HttpGet("GetFolderData/{id}")]
        public async Task<ActionResult<Folder>> GetFolderData(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return BadRequest(new { message = "Please login First." });
            }

            var folder = await _context.Folders
               .Where(p => p.FolderId == id)
               .Select(p => new FolderDataDto
               {
                   FolderId= p.FolderId,
                   FolderName = p.FolderName,
                   FolderPath = p.FolderPath,
                   creator = p.User.UserName,
                   ExcelSheetUrl = Url.Action(nameof(GetExcelSheet), new { id }),
                   FacesFolderUrl = Url.Action(nameof(GetFacesFolder), new { id }),
                   VoicesFolderUrl = Url.Action(nameof(GetVoicesFolder), new { id })
               })
               .FirstOrDefaultAsync();

            if (folder == null)
                return NotFound();

            var subFolders = await _context.Folders.Where(f => f.ParentFolderId == id).ToListAsync();
            var sessions = await _context.Sessions.Where(r => r.Folder_Id == id).ToListAsync();

            return Ok(new { Folder = folder, SubFolders = subFolders, Sessions = sessions });
        }

        // POST: api/Folders
        [HttpPost("CreateFolder")]
        public async Task<ActionResult<Folder>> CreateFolder([FromForm] FolderDto folderDto, [FromQuery] int? id)
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

            var folder = new Folder
            {
                FolderName = folderDto.FolderName
            };
            folder.User_Id = userId;
            folder.createdAt = DateTime.UtcNow;
            folder.ParentFolderId = id;
            folder.FolderPath = $"~/{folder.FolderName}";
            if (id != null)
            {
                var parentFolder = await _context.Folders.FindAsync(id);
                folder.ParentFolder = parentFolder;
                folder.FolderPath = $"{parentFolder.FolderPath}/{folder.FolderName}";
               
                if (folderDto.FacesFolder != null)
                {
                    using var stream1 = new MemoryStream();
                    await folderDto.FacesFolder.CopyToAsync(stream1);
                    folder.FacesFolder = stream1.ToArray();
                }
                else
                {
                    folder.FacesFolder = parentFolder.FacesFolder;
                }
                if (folderDto.VoicesFolder != null)
                {
                    using var stream2 = new MemoryStream();
                    await folderDto.VoicesFolder.CopyToAsync(stream2);
                    folder.VoicesFolder = stream2.ToArray();
                }
                else
                {
                    folder.VoicesFolder = parentFolder.VoicesFolder;

                }
                if (folderDto.Sheet != null)
                {
                    using var stream3 = new MemoryStream();
                    await folderDto.Sheet.CopyToAsync(stream3);
                    folder.Sheet = stream3.ToArray();
                }
                else
                {
                    folder.Sheet = parentFolder.Sheet;
                }
               
            }
            else 
            {
                if (folderDto.FacesFolder != null)
                {
                    using var stream1 = new MemoryStream();
                    await folderDto.FacesFolder.CopyToAsync(stream1);
                    folder.FacesFolder = stream1.ToArray();
                }
                if (folderDto.VoicesFolder != null)
                {
                    using var stream2 = new MemoryStream();
                    await folderDto.VoicesFolder.CopyToAsync(stream2);
                    folder.VoicesFolder = stream2.ToArray();
                }
                if (folderDto.Sheet != null)
                {
                    using var stream3 = new MemoryStream();
                    await folderDto.Sheet.CopyToAsync(stream3);
                    folder.Sheet = stream3.ToArray();
                }
                else 
                {
                    return BadRequest(new { message = "upload excel sheet,please." });
                }
                if(folderDto.FacesFolder == null && folderDto.VoicesFolder == null && folderDto.Sheet == null) 
                {
                    return BadRequest(new { message = "upload one file at least." });
                }
               
            }
            _context.Add(folder);
            await _context.SaveChangesAsync();

            //return Ok(new { message = "Folder created successfully." });
            if (id != null)
            {
                return CreatedAtAction(nameof(GetFolderData), new { id = folder.ParentFolderId }, folder);

            }
            return CreatedAtAction(nameof(GetFolderData), new { id = folder.FolderId }, folder);
        }

       


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateFolder(int id, [FromQuery] int? parentId, [FromForm] FolderDto folderDto)
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

            var folder = await _context.Folders.FindAsync(id);

            if (folder == null)
            {
                return NotFound();
            }

            if (folder.User_Id != userId)
            {
                return Unauthorized();
            }

            // Update folder name and path
            if (folderDto.FolderName != null)
            {
                folder.FolderName = folderDto.FolderName;
                folder.FolderPath = parentId == null
                    ? $"~/{folder.FolderName}"
                    : await GetFolderPathAsync(parentId) + $"/{folder.FolderName}";
            }

            // Update folder files and distribute to subfolders
            await UpdateFolderFilesAsync(folderDto, folder);

            // Start recursive update for subfolders and sessions
            await UpdateSubFoldersAndSessions(folder.FolderId, folder.FolderPath);

            _context.Folders.Update(folder);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Folder updated successfully." });
        }

        private async Task UpdateSubFoldersAndSessions(int folderId, string parentPath)
        {
            var subFolders = await _context.Folders
                .Where(f => f.ParentFolderId == folderId)
                .ToListAsync();

            foreach (var subFolder in subFolders)
            {
                // Update the path of the subfolder
                subFolder.FolderPath = $"{parentPath}/{subFolder.FolderName}";
                _context.Folders.Update(subFolder);

                // Update sessions associated with the subfolder
                var sessions = await _context.Sessions
                    .Where(s => s.Folder_Id == subFolder.FolderId)
                    .ToListAsync();

                foreach (var session in sessions)
                {
                    if (subFolder.Sheet != session.Sheet)
                    {
                        session.Sheet = subFolder.Sheet;
                    }
                    if (subFolder.FacesFolder != session.FacesFolder)
                    {
                        session.FacesFolder = subFolder.FacesFolder;
                    }
                    if (subFolder.VoicesFolder != session.VoicesFolder)
                    {
                        session.VoicesFolder = subFolder.VoicesFolder;
                    }
                    _context.Sessions.Update(session);
                }

                // Recursively update subfolders
                await UpdateSubFoldersAndSessions(subFolder.FolderId, subFolder.FolderPath);
            }

            // Update sessions of the current folder
            var sessionsForCurrentFolder = await _context.Sessions
                .Where(s => s.Folder_Id == folderId)
                .ToListAsync();

            var folder = await _context.Folders.FindAsync(folderId);

            foreach (var session in sessionsForCurrentFolder)
            {
                if (folder.Sheet != session.Sheet)
                {
                    session.Sheet = folder.Sheet;
                }
                if (folder.FacesFolder != session.FacesFolder)
                {
                    session.FacesFolder = folder.FacesFolder;
                }
                if (folder.VoicesFolder != session.VoicesFolder)
                {
                    session.VoicesFolder = folder.VoicesFolder;
                }
                _context.Sessions.Update(session);
            }

            await _context.SaveChangesAsync();
        }
        private async Task UpdateFolderFilesAsync(FolderDto folderDto, Folder folder)
        {
            // Handle file uploads
            if (folderDto.FacesFolder != null)
            {
                var fileData = await ConvertToByteArrayAsync(folderDto.FacesFolder);
                folder.FacesFolder = fileData;

                // Distribute the file to subfolders
                await DistributeFileToSubfoldersAsync(folder.FolderId, fileData, "FacesFolder");
            }

            if (folderDto.VoicesFolder != null)
            {
                var fileData = await ConvertToByteArrayAsync(folderDto.VoicesFolder);
                folder.VoicesFolder = fileData;

                // Distribute the file to subfolders
                await DistributeFileToSubfoldersAsync(folder.FolderId, fileData, "VoicesFolder");
            }

            if (folderDto.Sheet != null)
            {
                var fileData = await ConvertToByteArrayAsync(folderDto.Sheet);
                folder.Sheet = fileData;

                // Distribute the file to subfolders
                await DistributeFileToSubfoldersAsync(folder.FolderId, fileData, "Sheet");
            }
        }

        private async Task DistributeFileToSubfoldersAsync(int parentFolderId, byte[] fileData, string fileType)
        {
            var subFolders = await _context.Folders
                .Where(f => f.ParentFolderId == parentFolderId)
                .ToListAsync();

            foreach (var subFolder in subFolders)
            {
                // Update the file data in the subfolder
                switch (fileType)
                {
                    case "Sheet":
                        subFolder.Sheet = fileData;
                        break;
                    case "FacesFolder":
                        subFolder.FacesFolder = fileData;
                        break;
                    case "VoicesFolder":
                        subFolder.VoicesFolder = fileData;
                        break;
                }

                _context.Folders.Update(subFolder);

                // Recursively distribute to subfolders of the current subfolder
                await DistributeFileToSubfoldersAsync(subFolder.FolderId, fileData, fileType);
            }

            await _context.SaveChangesAsync();
        }

        private async Task<byte[]> ConvertToByteArrayAsync(IFormFile file)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }

        private async Task<string> GetFolderPathAsync(int? folderId)
        {
            var folder = await _context.Folders.FindAsync(folderId);
            if (folder == null)
            {
                return "";
            }

            return folder.ParentFolderId == null
                ? $"~/{folder.FolderName}"
                : await GetFolderPathAsync(folder.ParentFolderId.Value) + $"/{folder.FolderName}";
        }

        // DELETE: api/Folders/5
        //[HttpDelete("{id}")]
        //public async Task<IActionResult> DeleteFolder(int id)
        //{
        //    var folder = await _context.Folders.FindAsync(id);
        //    if (folder == null)
        //        return NotFound();

        //    // Recursively delete subfolders
        //    await DeleteSubFoldersAndSessions(id);

        //    _context.Folders.Remove(folder);
        //    await _context.SaveChangesAsync();

        //    return Ok(new { message = "Folder deleted successfully." });
        //}



        //private async Task DeleteSubFoldersAndSessions(int folderId)
        //{
        //    var subFolders = await _context.Folders.Where(f => f.ParentFolderId == folderId).ToListAsync();
        //    var sessions= await _context.Sessions.Where(f => f.Folder_Id == folderId).ToListAsync();
        //    foreach (var session in sessions)
        //    {
        //        var recordList = await _context.AttendanceRecords
        //         .Where(r => r.SessionId == session.SessionId)
        //         .ToListAsync();
        //        _context.AttendanceRecords.RemoveRange(recordList);
        //        await _context.SaveChangesAsync();
        //    }            
        //    _context.Sessions.RemoveRange(sessions);
        //    await _context.SaveChangesAsync();
        //    foreach (var subFolder in subFolders)
        //    {
        //        await DeleteSubFoldersAndSessions(subFolder.FolderId);
        //        _context.Folders.Remove(subFolder);
        //    }
        //}

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFolder(int id)
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

            var folder = await _context.Folders.FindAsync(id);
            if (folder == null)
                return NotFound();

            // Start the recursive deletion process
            await DeleteSubFoldersAndSessions(id);

            // Remove the folder itself
            _context.Folders.Remove(folder);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Folder and its contents deleted successfully." });
        }

        private async Task DeleteSubFoldersAndSessions(int folderId)
        {
            // Delete sessions associated with the current folder
            var sessions = await _context.Sessions
                .Where(s => s.Folder_Id == folderId)
                .ToListAsync();

            foreach (var session in sessions)
            {
                // Delete attendance records associated with the session
                var attendanceRecords = await _context.AttendanceRecords
                    .Where(r => r.SessionId == session.SessionId)
                    .ToListAsync();

                _context.AttendanceRecords.RemoveRange(attendanceRecords);
                await _context.SaveChangesAsync();
            }

            // Remove sessions from the database
            _context.Sessions.RemoveRange(sessions);
            await _context.SaveChangesAsync();

            // Recursively delete subfolders
            var subFolders = await _context.Folders
                .Where(f => f.ParentFolderId == folderId)
                .ToListAsync();

            foreach (var subFolder in subFolders)
            {
                await DeleteSubFoldersAndSessions(subFolder.FolderId);
                _context.Folders.Remove(subFolder);
            }

            await _context.SaveChangesAsync();
        }

        private bool FolderExists(int id)
        {
            return _context.Folders.Any(e => e.FolderId == id);
        }
        [HttpGet("download/sheet/{id}")]
        public async Task<IActionResult> GetExcelSheet(int id)
        {
            var folder = await _context.Folders.FindAsync(id);
            if (folder == null)
            {
                return NotFound();
            }

            if (folder.Sheet == null)
            {
                return NotFound("Excel sheet not found.");
            }

            return File(folder.Sheet, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "sheet.xlsx");
        }

        [HttpGet("download/facesfolder/{id}")]
        public async Task<IActionResult> GetFacesFolder(int id)
        {
            var folder = await _context.Folders.FindAsync(id);
            if (folder == null)
            {
                return NotFound();
            }

            if (folder.FacesFolder == null)
            {
                return NotFound("Faces folder file not found.");
            }

            return File(folder.FacesFolder, "application/x-rar-compressed", "facesfolder.rar");
        }

        [HttpGet("download/voicesfolder/{id}")]
        public async Task<IActionResult> GetVoicesFolder(int id)
        {
            var folder = await _context.Folders.FindAsync(id);
            if (folder == null)
            {
                return NotFound();
            }

            if (folder.VoicesFolder == null)
            {
                return NotFound("Voices folder file not found.");
            }

            return File(folder.VoicesFolder, "application/x-rar-compressed", "voicesfolder.rar");
        }
    }
}
