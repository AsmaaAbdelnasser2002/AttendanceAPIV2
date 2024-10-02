using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AttendanceAPIV2.Enums;

namespace AttendanceAPIV2.Models
{
    public class Session
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SessionId { get; set; }

        [Required]
        [StringLength(100)]
        public string SessionName { get; set; }

        [StringLength(255)]
        public string SessionPlace { get; set; }

        [StringLength(100)]
        public string SessionDescription { get; set; }
        
        
        public byte[]? Sheet { get; set; }

        public byte[]? FacesFolder { get; set; }

        public byte[]? VoicesFolder { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        public DateTime TimeLimit { get; set; }


        public ExpiredSession Expired { get; set; }

        public int? ExamId { get; set; } 

        [ForeignKey("User")]
        public string? User_Id { get; set; }
        public virtual User? User { get; set; }

        [ForeignKey("Folder")]
        public int? Folder_Id { get; set; }
        public virtual Folder? Folder { get; set; }
        public virtual List<AttendanceRecord> AttendanceRecords { get; set; }

    }
}
