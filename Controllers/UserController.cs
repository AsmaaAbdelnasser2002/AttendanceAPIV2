using Microsoft.AspNetCore.Mvc;
using AttendanceAPIV2.Models;
using AttendanceAPIV2.Models.DTOs;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;



namespace AttendanceAPIV2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        public UsersController(UserManager<User> userManager,IConfiguration configuration)
        {
            _userManager = userManager;
            this.configuration = configuration;
        }
        private readonly UserManager<User> _userManager;
        private readonly IConfiguration configuration;

        // POST: api/Users
        [HttpPost("Register")]
        public async Task<IActionResult> CreateUser([FromForm]UserDto userDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            User user=new()
            {
                UserName = userDto.Username,
                Email = userDto.Email,
                Age = userDto.Age,
                Gender = userDto.Gender,
                UserRole = userDto.UserRole
            };
            var EmailExists = await _userManager.Users
                            .FirstOrDefaultAsync(p => p.Email == userDto.Email);

            if (EmailExists != null)
            {
                return BadRequest(new { message = "An Email is already exists." });
            }
            IdentityResult result = await _userManager.CreateAsync(user,userDto.UserPassword);
            if (result.Succeeded)
            {
                return Ok("Succeeded");
            }
            else
            {
                foreach (var item in result.Errors)
                {
                    ModelState.AddModelError("",item.Description);
                }
                return BadRequest(ModelState);
            }



        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] LoginDto loginDto)
        {

            if (ModelState.IsValid)
            {
                User? user = await _userManager.FindByEmailAsync(loginDto.Email);
                if (user != null)
                {
                    if (await _userManager.CheckPasswordAsync(user,loginDto.UserPassword))
                    {
                        var claims = new List<Claim>();
                        claims.Add(new Claim(ClaimTypes.Name, user.UserName));
                        claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id));
                        claims.Add(new Claim(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString()));
                        var roles =await _userManager.GetRolesAsync(user);
                        foreach (var role in roles)
                        {
                            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
                        }
                        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:SecretKey"]));
                        var sc = new SigningCredentials(key,SecurityAlgorithms.HmacSha256);
                        var token = new JwtSecurityToken(
                            claims: claims,
                            issuer: configuration["JWT:Issuer"],
                            audience: configuration["JWT:Audience"],
                            expires: DateTime.Now.AddDays(1),
                            signingCredentials: sc
                            ) ;
                        var _token = new
                        {
                           token= new JwtSecurityTokenHandler().WriteToken(token),
                           expiration=token.ValidTo,
                           userId = user.Id
                        };
                        return Ok( _token );
                    }
                    else 
                    {
                        return Unauthorized();  
                    }
                }
                else
                {
                    ModelState.AddModelError("","Email is invalid.");
                }
            }
            return BadRequest(ModelState);
        }

        
    }
}
