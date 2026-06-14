namespace Iroh.Models.DTOs.Auth
{
    // Register yanıtı — User entity'si (şifre hash dahil) asla serialize edilmez.
    public class UserResponseDto
    {
        public int id { get; set; }
        public required string name { get; set; }
        public string? lastname { get; set; }
        public string? mail { get; set; }
        public string? phone { get; set; }
        public bool isActive { get; set; }
    }
}
