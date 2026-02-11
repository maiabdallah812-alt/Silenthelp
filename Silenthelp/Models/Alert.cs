using SilentHelp.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SilentHelp.Models
{
    public class Alert
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ChildId { get; set; }

        [Required, MaxLength(10)]
        public string TriggerType { get; set; } = string.Empty; // "button", "voice", "shake"

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        [MaxLength(500)]
        public string? AudioUrl { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "active"; // "active", "acknowledged", "resolved"

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? AcknowledgedAt { get; set; }

        // Navigation
        [ForeignKey("ChildId")]
        public User Child { get; set; } = null!;
    }
}