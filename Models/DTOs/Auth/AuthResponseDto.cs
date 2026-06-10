namespace Iroh.Models.DTOs.Auth
{
    public class AuthResponseDto
    {
        public string accessToken { get; set; } = string.Empty;
        public string refreshToken { get; set; } = string.Empty;
        public double expiresIn { get; set; }
        public double refreshExpiresIn { get; set; }
    }
}
