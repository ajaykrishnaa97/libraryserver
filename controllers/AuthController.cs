using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Library.Models;
using Library.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Library.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly LibraryContext _context;
        private readonly IConfiguration _config;
        private readonly EmailService _emailService;

        public AuthController(LibraryContext context, IConfiguration config, EmailService emailService)
        {
            _context = context;
            _config = config;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest("Email already registered.");

            var libraryExists = await _context.LibraryBranches.AnyAsync(l => l.Id == dto.LibraryId);
            if (!libraryExists)
                return BadRequest("Invalid library selected.");

            var user = new User
            {
                Name = dto.Name,
                PhoneNumber = dto.PhoneNumber,
                Address = dto.Address,
                LibraryId = dto.LibraryId,
                Email = dto.Email,
                Role = dto.Role,
                IsApproved = false,
                PasswordHash = null,
                PasswordResetToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                PasswordResetTokenExpires = DateTime.UtcNow.AddHours(1)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var setPasswordLink = $"http://localhost:3000/set-password?token={Uri.EscapeDataString(user.PasswordResetToken)}";

            await _emailService.SendEmailAsync(
                user.Email,
                "Set Your Password - FreshReads Library",
                $@"
                    <p>Hello,<br/><br/>
                    Thanks for registering. Please <a href='{setPasswordLink}'>click here</a> to set your password and activate your account.</p>
                "
            );

            return Ok("Registration successful. Please check your email to set your password.");
        }

        [HttpPost("set-password")]
        public async Task<IActionResult> SetPassword([FromBody] SetPasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.PasswordResetToken == dto.Token &&
                u.PasswordResetTokenExpires > DateTime.UtcNow
            );

            if (user == null)
                return BadRequest("Invalid or expired token.");

            if (user.PasswordHash != null)
                return BadRequest("Password is already set. Use the forgot password option if you need to reset.");

            user.PasswordHash = HashPassword(dto.Password);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpires = null;

            await _context.SaveChangesAsync();

            return Ok("Password set successfully. You can now log in once your account is approved.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null)
                return Unauthorized("Invalid credentials.");

            if (user.PasswordHash == null)
                return Unauthorized("Please set your password before logging in.");

            if (user.PasswordHash != HashPassword(dto.Password))
                return Unauthorized("Invalid credentials.");

            if (!user.IsApproved)
                return Unauthorized("Your account is awaiting approval by a librarian.");

            var token = GenerateJwtToken(user);

            return Ok(new
            {
                token,
                user = new { user.Id, user.Email, user.Role }
            });
        }

        [Authorize(Roles = "Librarian")]
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .Include(u => u.LibraryBranch)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Role,
                    u.IsApproved,
                    Library = new
                    {
                        u.LibraryBranch.Id,
                        u.LibraryBranch.Name,
                        u.LibraryBranch.Location
                    }
                })
                .ToListAsync();

            return Ok(users);
        }

        [Authorize(Roles = "Librarian")]
        [HttpPost("approve/{userId}")]
        public async Task<IActionResult> ApproveUser(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            user.IsApproved = true;
            await _context.SaveChangesAsync();

            return Ok("User approved.");
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null)
            {
                return Ok("If that email is registered, you'll receive a password reset link.");
            }

            user.PasswordResetToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            user.PasswordResetTokenExpires = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            var resetLink = $"http://localhost:3000/reset-password?token={Uri.EscapeDataString(user.PasswordResetToken)}";

            await _emailService.SendEmailAsync(
                user.Email,
                "Reset Your Password - FreshReads Library",
                $@"
                    <p>Hello {user.Name ?? ""},<br/><br/>
                    We received a request to reset your password. Please <a href='{resetLink}'>click here</a> to set a new password. This link is valid for 1 hour.<br/><br/>
                    <strong>If you have never set a password before, you can also use this link to set it now.</strong><br/><br/>
                    If you did not request this, please ignore this email.</p>
                "
            );

            return Ok("If that email is registered, you'll receive a password reset link.");
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] SetPasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.PasswordResetToken == dto.Token &&
                u.PasswordResetTokenExpires > DateTime.UtcNow
            );

            if (user == null)
                return BadRequest("Invalid or expired token.");

            user.PasswordHash = HashPassword(dto.Password);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpires = null;

            await _context.SaveChangesAsync();

            return Ok("Your password has been reset successfully. You can now log in if your account has been approved by a librarian.");
        }

        [HttpGet("validate-reset-token")]
        public async Task<IActionResult> ValidateResetToken([FromQuery] string token)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.PasswordResetToken == token &&
                u.PasswordResetTokenExpires > DateTime.UtcNow
            );

            if (user == null)
            {
                return BadRequest("Invalid or expired token.");
            }

            return Ok("Valid token.");
        }

        [HttpGet("libraries")]
        public async Task<IActionResult> GetLibraries()
        {
            var libraries = await _context.LibraryBranches
                .Select(l => new { l.Id, l.Name, l.Location })
                .ToListAsync();

            return Ok(libraries);
        }

        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? "default-key"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(2),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
