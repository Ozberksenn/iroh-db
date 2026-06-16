namespace Iroh.Models.DTOs.Auth
{
    public class UserRegisterDto
    {
        public required string Name { get; set; }
        public string? LastName { get; set; }
        public required string Mail { get; set; }
        public required string Password { get; set; }
        public string? Phone { get; set; }
    }
}
