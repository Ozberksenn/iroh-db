

using Iroh.Models.Enums;

namespace Iroh.Models.DTOs.Auth
{
    public class RegisterDto
    {
        public required string name { get; set; }

        public required string lastname { get; set; }

        public required string mail { get; set; }

        public required string password { get; set; }

        public string? phone { get; set; }

    }
}