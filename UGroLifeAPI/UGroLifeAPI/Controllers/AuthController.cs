using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using UGroLifeAPI.Models;
using UGroLifeAPI.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; 
using Microsoft.IdentityModel.Tokens; 
using System.IdentityModel.Tokens.Jwt; 
using System.Text;
using MimeKit; 
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration; 

namespace UGroLifeAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly string _sendingGmailAddress = "lineandleaf25@gmail.com"; 
        private readonly string _gmailAppPassword = "yrcy krxb izun whhp";
        public AuthController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration; 
        }

        [HttpPost("register")]
        public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new RegisterResponse
                {
                    Success = false,
                    Message = "Validation failed. Please check your inputs.",
                });
            }

            var existingUser = await _context.Users.AnyAsync(u => u.Email == request.Email);
            if (existingUser)
            {
                return Conflict(new RegisterResponse { Success = false, Message = "Email already registered." });
            }

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var newUser = new User
            {
                FullName = request.FullName,
                Email = request.Email,
                Mobile = request.Mobile,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            try
            {
                await SendRegistrationConfirmationEmail(newUser.Email, newUser.FullName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending registration email to {newUser.Email}: {ex.Message}");
            }
            return Ok(new RegisterResponse { Success = true, Message = "Registration successful! You can now log in." });
        }
        private async Task SendRegistrationConfirmationEmail(string recipientEmail, string recipientName)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("UGroLife Team", _sendingGmailAddress));
            message.To.Add(new MailboxAddress(recipientName, recipientEmail));
            message.Subject = "Welcome to UGroLife! Your Registration is Complete.";

            var bodyBuilder = new BodyBuilder();
            bodyBuilder.HtmlBody = $@"
            <p>Dear {recipientName},</p>
            <p>Thank you for registering with UGroLife! We are thrilled to have you join our community.</p>
            <p>At UGroLife, we are committed to bringing you the freshest, most nutritious microgreens directly from our farm in Chennai. Get ready to enhance your health and culinary experiences with our vibrant greens.</p>
            <p>You can now log in to your account and start exploring our wide range of microgreens:</p>
            <p><a href=""https://UGroLife.com/login"" style=""display: inline-block; padding: 10px 20px; background-color: #0cae00; color: #ffffff; text-decoration: none; border-radius: 5px;"">Log In to Your Account</a></p>
            <p>If you have any questions, feel free to contact us at <a href=""mailto:info@UGroLife.com"">info@UGroLife.com</a> or reply to this email.</p>
            <p>Happy growing and healthy eating!</p>
            <p>Best regards,<br>The UGroLife Team</p>
            <p><img src=""https://placehold.co/100x100/4CAF50/FFFFFF?text=🌱"" alt=""UGroLife Logo"" style=""border-radius: 50%;""></p>
        ";
            message.Body = bodyBuilder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_sendingGmailAddress, _gmailAppPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new LoginResponse
                {
                    Success = false,
                    Message = "Invalid login request. Please check your inputs.",
                });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.EmailOrUsername);

            if (user == null)
            {
                return Unauthorized(new LoginResponse { Success = false, Message = "Invalid credentials." });
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                return Unauthorized(new LoginResponse { Success = false, Message = "Invalid credentials." });
            }

            var token = GenerateJwtToken(user);

            return Ok(new LoginResponse
            {
                Success = true,
                Message = "Login successful!",
                Token = token,
                FullName = user.FullName, 
                Email = user.Email 
            });
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email), 
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), 
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), 
                new Claim(ClaimTypes.Email, user.Email), 
                new Claim(ClaimTypes.Name, user.FullName) 
            };

            var jwtSecret = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtSecret))
            {
                throw new InvalidOperationException("JWT Key not found in configuration. Please add it to appsettings.json");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expires = DateTime.UtcNow.AddHours(Convert.ToDouble(_configuration["Jwt:ExpireHours"]));

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}