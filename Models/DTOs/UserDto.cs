using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AttendanceAPIV2.Models.DTOs
{
    public class UserDto
    {
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(50)]
        [RegularExpression(@"^[a-zA-Z]+[ a-zA-Z-_]*$", ErrorMessage = "Use Characters only")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [StringLength(100)]
        [RegularExpression(".+@.+\\..+", ErrorMessage = "please enter correct email address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [RegularExpression(@"^(?=.*\d)(?=.*[@$!%#*?&])[A-Za-z\d@$!%#*?&]{8,}$", ErrorMessage = "Enter a strong password that contains 8 English letters, a number and a special symbol(&@#%!).")]
        public string UserPassword { get; set; }

        

        [Required(ErrorMessage = "Your age is required. ")]
        [Range(18, 80)]
        public int Age { get; set; }

        [Required]
        [RegularExpression(@"^(Male|Female)$", ErrorMessage = "Invalid gender. Valid gender are 'Male', 'Female'.")]

        public string Gender { get; set; }

        [Required]
        [RegularExpression(@"^(Instructor|Attender|Admin)$", ErrorMessage = "Invalid user role. Valid roles are 'Instructor', 'Attender', 'Admin'.")]
        //Instructor or Attender or Admin
        public string UserRole { get; set; }
    }
}
