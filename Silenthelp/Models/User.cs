using System.ComponentModel.DataAnnotations;

namespace SilentHelp.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required, MaxLength(10)]
        public string Role { get; set; } = "parent"; // "parent" or "child"

        [MaxLength(20)]
        public string? Phone { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? DeviceToken { get; set; }

        // Navigation properties
        public ICollection<FamilyLink> ParentLinks { get; set; } = new List<FamilyLink>();
        public ICollection<FamilyLink> ChildLinks { get; set; } = new List<FamilyLink>();
        public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    }
}