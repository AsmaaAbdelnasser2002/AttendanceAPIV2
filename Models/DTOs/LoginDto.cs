using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AttendanceAPIV2.Models.DTOs
{
    public class LoginDto
    {
        [Required(ErrorMessage = "Username or Email is required.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        public string UserPassword { get; set; }
    }
}
