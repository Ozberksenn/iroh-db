namespace Iroh.Models.DTOs.Auth
{
    public class UserRegisterDto
    {
        public required string name { get; set; }
        public string? lastName { get; set; }
        public required string mail { get; set; }
        public required string password { get; set; }
        public string? phone { get; set; }
    }
}
