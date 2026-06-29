using Iroh.Models.DTOs.Auth;
using Iroh.Models.Entities;
using Iroh.Models.Responses;
using Iroh.Services;
using Microsoft.AspNetCore.Mvc;

namespace Iroh.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IWebHostEnvironment _env;

        public AuthController(IAuthService authService, IWebHostEnvironment env)
        {
            _authService = authService;
            _env = env;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            var authResponse = await _authService.Login(loginDto.Mail, loginDto.Password);
            if (authResponse == null)
            {
                return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "E-posta veya şifre hatalı!");
            }

            SetRefreshTokenCookie(authResponse.RefreshToken);
            return Ok(ApiResponse.Ok(authResponse, "Giriş başarılı"));
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto refreshTokenDto)
        {
            string? token = refreshTokenDto.RefreshToken;
            if (string.IsNullOrEmpty(token))
            {
                token = Request.Cookies["refreshToken"];
            }

            if (string.IsNullOrEmpty(token))
            {
                return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Refresh token bulunamadı!");
            }

            var authResponse = await _authService.RefreshToken(token);
            if (authResponse == null)
            {
                return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Geçersiz veya süresi dolmuş token!");
            }

            SetRefreshTokenCookie(authResponse.RefreshToken);
            return Ok(ApiResponse.Ok(authResponse, "Token başarıyla yenilendi"));
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegisterDto registerDto)
        {
            // Dup e-posta → AuthService BusinessRuleException atar → handler 400 ProblemDetails.
            var createdUser = await _authService.Register(new User
            {
                Name = registerDto.Name,
                LastName = registerDto.LastName,
                Mail = registerDto.Mail,
                Password = registerDto.Password,
                Phone = registerDto.Phone,
                IsActive = true,
                Role = Iroh.Models.Enums.UserRole.User // Default to User role on public registration
            });

            var dto = new UserResponseDto
            {
                Id = createdUser.Id,
                Name = createdUser.Name,
                LastName = createdUser.LastName,
                Mail = createdUser.Mail,
                Phone = createdUser.Phone,
                IsActive = createdUser.IsActive,
                Role = createdUser.Role
            };
            return Ok(ApiResponse.Ok(dto, "Kullanıcı başarıyla oluşturuldu"));
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            var isDev = _env.IsDevelopment();
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                // Prod (HTTPS, cross-site): Secure + SameSite=None. Dev (HTTP): Secure=false + Lax ki cookie set edilebilsin.
                Secure = !isDev,
                SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None,
                Expires = DateTime.UtcNow.AddDays(7)
            };
            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }
    }
}
