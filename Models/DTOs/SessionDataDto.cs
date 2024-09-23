using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AttendanceAPIV2.Models.DTOs
{
    public class SessionDataDto
    {
        public int SessionId { get; set; }
        public string SessionName { get; set; }
        public string SessionPlace { get; set; }


        public string SessionDescription { get; set; }


        public DateTime StartTime { get; set; }


        public DateTime EndTime { get; set; }

        public DateTime TimeLimit { get; set; }

        public string creator { get; set; }


        public string ExcelSheetUrl { get; set; }
        public string FacesFolderUrl { get; set; }
        public string VoicesFolderUrl { get; set; }
    }
}
