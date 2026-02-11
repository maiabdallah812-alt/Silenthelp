using System.ComponentModel.DataAnnotations;

namespace SilentHelp.Models.DTOs
{
    public class RegisterDto
    {
        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "parent"; // "parent" or "child"

        public string? Phone { get; set; }
    }
}
