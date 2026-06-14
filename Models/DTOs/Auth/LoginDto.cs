namespace Iroh.Models.DTOs.Auth
{
    public class LoginDto
    {
        public required string Mail { get; set; }
        public required string Password { get; set; }
    }
}
