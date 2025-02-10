using System.ComponentModel.DataAnnotations;

namespace AttendanceAPIV2.Models.DTOs
{
    public class FolderDto
    {
        [Required]
        public string FolderName { get; set; }

        public IFormFile? Sheet { get; set; }

        public IFormFile? FacesFolder { get; set; }

        public IFormFile? VoicesFolder { get; set; }
    }
}
