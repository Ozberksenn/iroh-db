using Iroh.Models.Enums;

namespace Iroh.Models.DTOs.Auth
{
    // Register yanıtı — User entity'si (şifre hash dahil) asla serialize edilmez.
    public class UserResponseDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? LastName { get; set; }
        public string? Mail { get; set; }
        public string? Phone { get; set; }
        public bool IsActive { get; set; }
        public UserRole Role { get; set; }
    }
}
