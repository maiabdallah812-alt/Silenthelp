using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SilentHelp.Models
{
    public class FamilyLink
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ParentId { get; set; }

        public Guid? ChildId { get; set; } // Null until child joins

        [Required, MaxLength(8)]
        public string InviteCode { get; set; } = Guid.NewGuid().ToString("N")[..8];

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("ParentId")]
        public User Parent { get; set; } = null!;

        [ForeignKey("ChildId")]
        public User? Child { get; set; }
    }
}