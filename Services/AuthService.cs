using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Iroh.Models.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Iroh.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public string? Login(string mail, string password)
        {
            var user = _context.User.FirstOrDefault(u => u.mail == mail && u.isActive);
            
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.password))
            {
                return null;
            }

            return GenerateJwtToken(user);
        }

        public User Register(User user)
        {
            // Mail adresi kullanımda mı kontrol et
            if (_context.User.Any(u => u.mail == user.mail))
            {
                throw new InvalidOperationException("Bu e-posta adresi zaten kullanımda!");
            }

            // Şifreyi hash'le
            user.password = BCrypt.Net.BCrypt.HashPassword(user.password);
            user.isActive = true;

            _context.User.Add(user);
            _context.SaveChanges();
            return user;
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.mail),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("name", user.name)
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(Convert.ToDouble(jwtSettings["ExpiryInMinutes"])),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
