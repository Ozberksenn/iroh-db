using Iroh.Models.CustomResponses;
using Iroh.Models.DTOs.Auth;
using Iroh.Models.Entities;
using Iroh.Services;
using Microsoft.AspNetCore.Mvc;

namespace Iroh.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginDto loginDto)
        {
            var authResponse = _authService.Login(loginDto.mail, loginDto.password);

            if (authResponse == null)
            {
                return Unauthorized(new CustomResponse<string>(false, "E-posta veya şifre hatalı!", null));
            }

            SetRefreshTokenCookie(authResponse.refreshToken);

            return Ok(new CustomResponse<AuthResponseDto>(true, "Giriş başarılı", authResponse));
        }

        [HttpPost("refresh")]
        public IActionResult Refresh([FromBody] RefreshTokenDto refreshTokenDto)
        {
            // Eğer body'de token yoksa cookie'den almayı dene
            string? token = refreshTokenDto.refreshToken;
            if (string.IsNullOrEmpty(token))
            {
                token = Request.Cookies["refreshToken"];
            }

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new CustomResponse<string>(false, "Refresh token bulunamadı!", null));
            }

            var authResponse = _authService.RefreshToken(token);

            if (authResponse == null)
            {
                return Unauthorized(new CustomResponse<string>(false, "Geçersiz veya süresi dolmuş token!", null));
            }

            SetRefreshTokenCookie(authResponse.refreshToken);

            return Ok(new CustomResponse<AuthResponseDto>(true, "Token başarıyla yenilendi", authResponse));
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Frontend https ise true olmalı, Node.js tarafında true'ydu
                SameSite = SameSiteMode.None,
                Expires = DateTime.UtcNow.AddDays(7)
            };
            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] UserRegisterDto registerDto)
        {
            var user = new User
            {
                name = registerDto.name,
                lastname = registerDto.lastName,
                mail = registerDto.mail,
                password = registerDto.password,
                phone = registerDto.phone,
                isActive = true
            };

            try
            {
                var createdUser = _authService.Register(user);
                return Ok(new CustomResponse<User>(true, "Kullanıcı başarıyla oluşturuldu", createdUser));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new CustomResponse<string>(false, ex.Message, null));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new CustomResponse<string>(false, "Kayıt sırasında bir hata oluştu: " + ex.Message, null));
            }
        }
    }
}
