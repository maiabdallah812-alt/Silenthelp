using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SilentHelp.Data;
using SilentHelp.Models;
using SilentHelp.Models.DTOs;
using SilentHelp.Services;
using System.Security.Claims;
namespace Silenthelp.Api.Controllers

{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ITokenService _tokenService;

        public AuthController(AppDbContext db, ITokenService tokenService)
        {
            _db = db;
            _tokenService = tokenService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (dto.Role != "parent" && dto.Role != "child")
                return BadRequest(new { error = "Role must be 'parent' or 'child'" });

            if (await _db.Users.AnyAsync(u => u.Email == dto.Email.ToLower()))
                return BadRequest(new { error = "Email already registered" });

            var user = new User
            {
                FullName = dto.FullName,
                Email = dto.Email.ToLower(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = dto.Role,
                Phone = dto.Phone,
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var token = _tokenService.CreateToken(user);

            return Ok(new
            {
                token,
                user = new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.Role,
                }
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == dto.Email.ToLower());

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized(new { error = "Invalid email or password" });

            var token = _tokenService.CreateToken(user);

            return Ok(new
            {
                token,
                user = new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.Role,
                }
            });
        }


        [HttpPost("save-device-token")]
        [Authorize]
        public async Task<IActionResult> SaveDeviceToken([FromBody] SaveDeviceTokenRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            var userId = Guid.Parse(userIdClaim.Value);
            if (userId == null) return Unauthorized();

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            user.DeviceToken = request.DeviceToken;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Device token saved" });
        }
    }
}
    
