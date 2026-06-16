using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Iroh.Models.DTOs.Auth;
using Iroh.Models.Entities;
using Iroh.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Iroh.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> Login(string mail, string password);
        Task<AuthResponseDto?> RefreshToken(string refreshToken);
        Task<User> Register(User user);
    }

    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<AuthResponseDto?> Login(string mail, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Mail == mail && u.IsActive);
            
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                return null;
            }

            return GenerateAuthResponse(user);
        }

        public async Task<AuthResponseDto?> RefreshToken(string refreshToken)
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

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
                if (user == null) return null;

                return GenerateAuthResponse(user);
            }
            catch
            {
                return null;
            }
        }

        public async Task<User> Register(User user)
        {
            if (await _context.Users.AnyAsync(u => u.Mail == user.Mail))
            {
                throw new BusinessRuleException("Bu e-posta adresi zaten kullanımda!");
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
            user.IsActive = true;

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
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
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = Convert.ToDouble(jwtSettings["ExpiryInMinutes"]) * 60,
                RefreshExpiresIn = Convert.ToDouble(jwtSettings["RefreshExpiryInDays"]) * 24 * 60 * 60
            };
        }

        private string GenerateToken(User user, string secretKey, double expiryInMinutes)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Client JWT payload'unu ham olarak okuyor (core/session.ts -> parseJwt) ve "mail" + "id"
            // claim'lerini bekliyor; Node backend de token'ı { id, mail } ile imzalıyordu. Pariteyi koru.
            // "sub" ayrıca RefreshToken() tarafından okunduğu için bırakıldı.
            var claims = new[]
            {
                new Claim("id", user.Id.ToString()),
                new Claim("mail", user.Mail),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("name", user.Name)
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
