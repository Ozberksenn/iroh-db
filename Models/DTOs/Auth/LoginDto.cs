namespace Iroh.Models.DTOs.Auth
{
    public class LoginDto
    {
        public required string mail { get; set; }
        public required string password { get; set; }
    }
}
