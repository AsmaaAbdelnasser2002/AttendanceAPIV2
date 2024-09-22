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
using System.Drawing.Imaging;
using System.Drawing;
using AttendanceAPIV2.Enums;
using Newtonsoft.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;


[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FaceRecognitionController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AttendanceContext _context;
    private readonly HttpClient _httpClient;

    public FaceRecognitionController(HttpClient httpClient, IHttpClientFactory httpClientFactory, AttendanceContext context)
    {
        _httpClientFactory = httpClientFactory;
        _context = context;
        _httpClient = httpClient;
    }

    [HttpPost("train-model/{sessionId}")]
    public async Task<IActionResult> TrainModel([FromRoute] int sessionId)
    {
        var us = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (us == null)
        {
            return BadRequest(new { message = "Please login First." });
        }

        var sessionData = await _context.Sessions.FindAsync(sessionId);
        var instructorId = sessionData.User_Id;

        if (sessionData == null || sessionData.FacesFolder == null)
        {
            return NotFound(new { error = "Session or compressed images not found." });
        }

        // Send the compressed folder along with instructorId to the Flask API
        await ProcessAndUploadImages(sessionData.FacesFolder, instructorId);

        return Ok(new { message = "Training initiated successfully." });
    }

    private async Task UploadImagesToFlaskApi(string baseDirectory, string instructorId)
    {
        using (var client = _httpClientFactory.CreateClient())
        {
            var extractedFolder = Directory.GetDirectories(baseDirectory).FirstOrDefault();
            if (extractedFolder == null)
            {
                throw new DirectoryNotFoundException("No extracted folder found inside the base directory.");
            }

            var imageFiles = Directory.GetFiles(extractedFolder, "*.*", SearchOption.TopDirectoryOnly)
                                      .Where(file => file.EndsWith(".jpg") || file.EndsWith(".png") || file.EndsWith(".jpeg"))
                                      .ToList();

            if (imageFiles.Count == 0)
            {
                throw new FileNotFoundException("No image files found in the extracted folder.");
            }

            var form = new MultipartFormDataContent();

            

            // Add all image files to the form in one request
            foreach (var imageFile in imageFiles)
            {
                var userId = Path.GetFileNameWithoutExtension(imageFile); // Student ID

                try
                {
                    // Read the file as bytes and use ByteArrayContent to avoid stream disposal issues
                    byte[] imageBytes = await System.IO.File.ReadAllBytesAsync(imageFile);
                    var byteArrayContent = new ByteArrayContent(imageBytes);
                    byteArrayContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");

                    // Add each image to the form-data with user_id
                    form.Add(byteArrayContent, "files", Path.GetFileName(imageFile));
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to upload image {imageFile} for user {userId}: {ex.Message}");
                }
            }
            // Pass instructorId to the Flask API
            form.Add(new StringContent(instructorId), "user_id");
            // Send all images in a single request
            var response = await client.PostAsync("http://127.0.0.1:5000/upload-images", form);
            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Error uploading images: {errorResponse}");
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

    private async Task ProcessAndUploadImages(byte[] rarFileBytes, string instructorId)
    {
        var extractPath = Path.Combine(Path.GetTempPath(), "extracted_images");
        Directory.CreateDirectory(extractPath);

        // Extract the compressed RAR file to the temp directory
        ExtractRarFile(rarFileBytes, extractPath);

        // Upload each image file to the Flask API along with the instructorId
        await UploadImagesToFlaskApi(extractPath, instructorId);

        // Cleanup the extracted directory after upload
        Directory.Delete(extractPath, true);
    }


    [HttpPost("recognize-face/{sessionId}")]
    public async Task<IActionResult> RecognizeFace([FromRoute] int sessionId)
    {
        var us = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (us == null)
        {
            return BadRequest(new { message = "Please login First." });
        }

        var sessionData = await _context.Sessions.FindAsync(sessionId);
        var userId = sessionData.User_Id;
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest("User ID is required.");
        }

        // Open the camera and capture a frame
        using var capture = new VideoCapture(0); // 0 is the default camera
        using var frame = new Mat();

        if (!capture.IsOpened())
        {
            return StatusCode(500, "Camera not found or can't be opened.");
        }

        capture.Read(frame); // Capture a frame
        if (frame.Empty())
        {
            return StatusCode(500, "No image captured from camera.");
        }

        // Convert OpenCV Mat to a byte array (to send as a file)
        using var memoryStream = new MemoryStream();
        using (var bitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(frame))
        {
            bitmap.Save(memoryStream, ImageFormat.Jpeg); // Save the frame as JPEG to the memory stream
        }

        memoryStream.Position = 0;

        // Prepare the image to be sent in form-data
        var formData = new MultipartFormDataContent();
        

        var imageContent = new StreamContent(memoryStream);
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        formData.Add(imageContent, "file", "captured_frame.jpg"); // Ensure the key matches what Flask expects
        formData.Add(new StringContent(userId), "user_id");
        // Send the request to the Flask API
        var response = await _httpClient.PostAsync("http://127.0.0.1:5000/recognize-face", formData);

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        var responseData = await response.Content.ReadAsStringAsync();
        // Assuming you have the response content as a string
        var responseContent = await response.Content.ReadAsStringAsync();

        // Deserialize the response (you might be using a different method to do this)
        var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseContent);

        // Check if the response contains the recognized name
        if (jsonResponse.TryGetValue("recognized_name", out string recognizedName))
        {
            // Extract the user ID without the extension
            string userId2 = Path.GetFileNameWithoutExtension(recognizedName);

            var attendanceRecord = await _context.AttendanceRecords.FirstOrDefaultAsync(x => x.UserId == userId2 && x.SessionId == sessionId);
            if (attendanceRecord == null)
            {
                return NotFound("Attendance record not found for the specified user and session.");
            }

            // Modify the properties as needed
            attendanceRecord.TimeIn = DateTime.Now; // Update time in if needed
            attendanceRecord.Status = AttendanceStatus.Present; // Set the desired status                                      
            // Save the changes to the database
            await _context.SaveChangesAsync();

        }
       
        return Ok(responseData);
    }

}
