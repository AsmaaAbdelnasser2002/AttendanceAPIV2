using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AttendanceAPIV2.Models.DTOs
{
    public class SessionListDto
    {
        [Required]
        [StringLength(100)]
        public string SessionName { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        public String creator {  get; set; }
    }
}
