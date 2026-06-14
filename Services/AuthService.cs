using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Iroh.Models.DTOs.Auth;
using Iroh.Models.Entities;
using Iroh.Exceptions;
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

        public AuthResponseDto? Login(string mail, string password)
        {
            var user = _context.User.FirstOrDefault(u => u.mail == mail && u.isActive);
            
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.password))
            {
                return null;
            }

            return GenerateAuthResponse(user);
        }

        public AuthResponseDto? RefreshToken(string refreshToken)
        {
            try
            {
                var jwtSettings = _configuration.GetSection("JwtSettings");
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(jwtSettings["RefreshSecretKey"]!);

                tokenHandler.ValidateToken(refreshToken, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                var userId = int.Parse(jwtToken.Claims.First(x => x.Type == JwtRegisteredClaimNames.Sub).Value);

                var user = _context.User.FirstOrDefault(u => u.id == userId && u.isActive);
                if (user == null) return null;

                return GenerateAuthResponse(user);
            }
            catch
            {
                return null;
            }
        }

        public User Register(User user)
        {
            if (_context.User.Any(u => u.mail == user.mail))
            {
                throw new BusinessRuleException("Bu e-posta adresi zaten kullanımda!");
            }

            user.password = BCrypt.Net.BCrypt.HashPassword(user.password);
            user.isActive = true;

            _context.User.Add(user);
            _context.SaveChanges();
            return user;
        }

        private AuthResponseDto GenerateAuthResponse(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            
            var accessToken = GenerateToken(user, 
                jwtSettings["SecretKey"]!, 
                Convert.ToDouble(jwtSettings["ExpiryInMinutes"]));
            
            var refreshToken = GenerateToken(user, 
                jwtSettings["RefreshSecretKey"]!, 
                TimeSpan.FromDays(Convert.ToDouble(jwtSettings["RefreshExpiryInDays"])).TotalMinutes);

            return new AuthResponseDto
            {
                accessToken = accessToken,
                refreshToken = refreshToken,
                expiresIn = Convert.ToDouble(jwtSettings["ExpiryInMinutes"]) * 60,
                refreshExpiresIn = Convert.ToDouble(jwtSettings["RefreshExpiryInDays"]) * 24 * 60 * 60
            };
        }

        private string GenerateToken(User user, string secretKey, double expiryInMinutes)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
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
                expires: DateTime.UtcNow.AddMinutes(expiryInMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
