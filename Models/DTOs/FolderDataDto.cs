using System.ComponentModel.DataAnnotations;

namespace AttendanceAPIV2.Models.DTOs
{
    public class FolderDataDto
    {
        public int FolderId { get; set; }
        public string FolderName { get; set; }

        public string FolderPath { get; set; }

        public string creator { get; set; }

        public string ExcelSheetUrl { get; set; }
        public string FacesFolderUrl { get; set; }
        public string VoicesFolderUrl { get; set; }

        public DateTime createdAt { get; set; } = DateTime.UtcNow;
    }
}
