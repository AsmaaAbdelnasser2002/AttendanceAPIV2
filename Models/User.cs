﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace AttendanceAPIV2.Models
{
    public class User : IdentityUser
    {
        [Required(ErrorMessage = "Your age is required. ")]
        [Range(18, 80)]
        public int Age { get; set; }

        [Required]
        [RegularExpression(@"^(Male|Female)$", ErrorMessage = "Invalid gender. Valid gender are 'Male', 'Female'.")]

        public string Gender { get; set; }

        [Required]
        [RegularExpression(@"^(Instructor|Attender)$", ErrorMessage = "Invalid user role. Valid roles are 'Instructor', 'Attender'.")]
        //Instructor or Attender 
        public string UserRole { get; set; }

        public string? ExaminerId { get; set; }  

        public virtual List<AttendanceRecord> AttendanceRecords { get; set; }
        public virtual List<Folder> Sequances { get; set; }
        public virtual List<Session>? Sessions { get; set; }
    }
}
