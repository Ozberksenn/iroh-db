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
            var token = _authService.Login(loginDto.mail, loginDto.password);

            if (token == null)
            {
                return Unauthorized(new CustomResponse<string>(false, "E-posta veya şifre hatalı!", null));
            }

            return Ok(new CustomResponse<string>(true, "Giriş başarılı", token));
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
