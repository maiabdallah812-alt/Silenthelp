using System.ComponentModel.DataAnnotations;

namespace SilentHelp.Models.DTOs
{
    public class CreateAlertDto
    {
        [Required]
        public string TriggerType { get; set; } = string.Empty;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? AudioUrl { get; set; }
    }

    public class AlertResponseDto
    {
        public Guid Id { get; set; }
        public Guid ChildId { get; set; }
        public string ChildName { get; set; } = string.Empty;
        public string TriggerType { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? AudioUrl { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
    }
}
