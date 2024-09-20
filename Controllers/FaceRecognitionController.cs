using Microsoft.AspNetCore.Mvc;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using AttendanceAPIV2.Models;
using OpenCvSharp;



[ApiController]
[Route("api/[controller]")]
public class FaceRecognitionController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AttendanceContext _context;

    public FaceRecognitionController(IHttpClientFactory httpClientFactory, AttendanceContext context)
    {
        _httpClientFactory = httpClientFactory;
        _context = context;
    }

    [HttpPost("train-model/{sessionId}")]
    public async Task<IActionResult> TrainModel([FromRoute] int sessionId)
    {
        var sessionData = await _context.Sessions.FindAsync(sessionId);
        if (sessionData == null || sessionData.FacesFolder == null)
        {
            return NotFound(new { error = "Session or compressed images not found." });
        }

        await ProcessAndUploadImages(sessionData.FacesFolder);

        return Ok(new { message = "Training initiated successfully." });
    }

    private async Task UploadImagesToFlaskApi(string baseDirectory)
    {
        using (var client = _httpClientFactory.CreateClient())
        {
            var baseFolder = Directory.GetDirectories(baseDirectory).FirstOrDefault();
            if (baseFolder == null)
            {
                throw new DirectoryNotFoundException("No base folder found inside the extracted directory.");
            }

            var userFolders = Directory.GetDirectories(baseFolder);

            foreach (var userFolder in userFolders)
            {
                var userId = Path.GetFileName(userFolder);
                var form = new MultipartFormDataContent();
                form.Add(new StringContent(userId), "user_id");

                // Collect all files for this student
                var files = Directory.GetFiles(userFolder);
                if (files.Length == 0)
                {
                    continue; // Skip if no images are found
                }

                foreach (var filePath in files)
                {
                    var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var fileStreamContent = new StreamContent(fileStream);
                    fileStreamContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg"); // Adjust based on file type
                    form.Add(fileStreamContent, "files", Path.GetFileName(filePath));
                }

                // Send the form data with all images for the current student
                var response = await client.PostAsync("http://192.168.1.4:5000/upload-images", form);

                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Error uploading images for user {userId}: {errorResponse}");
                }
            }
        }
    }

    private void ExtractRarFile(byte[] rarFileBytes, string extractPath)
    {
        using (var stream = new MemoryStream(rarFileBytes))
        using (var archive = RarArchive.Open(stream))
        {
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                entry.WriteToDirectory(extractPath, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
            }
        }
    }

    private async Task ProcessAndUploadImages(byte[] rarFileBytes)
    {
        var extractPath = Path.Combine(Path.GetTempPath(), "extracted_images");
        Directory.CreateDirectory(extractPath);

        ExtractRarFile(rarFileBytes, extractPath);

        await UploadImagesToFlaskApi(extractPath);
    }


    

}