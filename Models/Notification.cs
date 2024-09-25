using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceAPIV2.Models
{
    public class Notification
    {

        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; }  // Reference to the user who will receive the notification

        public virtual User User { get; set; }

        public string Message { get; set; } 

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;  // To track if the user has read the notification

       
    }
}
