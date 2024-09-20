﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AttendanceAPIV2.Models.DTOs
{
    public class SessionDto
    {
        
        [Required]
        [StringLength(100)]
        public string SessionName { get; set; }

        [StringLength(255)]
        public string SessionPlace { get; set; }

        [StringLength(100)]
        public string SessionDescription { get; set; }

        public IFormFile? Sheet { get; set; }

        public IFormFile? FacesFolder { get; set; }

        public IFormFile? VoicesFolder { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        public DateTime TimeLimit { get; set; }

        //public string? NameOfSequance { get; set; }
    }
}
