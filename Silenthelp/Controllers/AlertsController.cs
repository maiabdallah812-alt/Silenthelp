using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SilentHelp.Data;
using SilentHelp.Hubs;
using SilentHelp.Models.DTOs;
using SilentHelp.Services;
using SilentHelp.Models;
using System.Security.Claims;

namespace Silenthelp.Api.Controllers
{
    // DTOs
    public class DetectEmergencyDto
    {
        public string? Text { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class CreateAlertDto
    {
        public string? TriggerType { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? AudioUrl { get; set; }
    }

    public class AlertResponseDto
    {
        public Guid Id { get; set; }
        public Guid ChildId { get; set; }
        public string? ChildName { get; set; }
        public string? TriggerType { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? AudioUrl { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
    }

    public class LoginDto
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AlertsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<AlertHub> _hubContext;
        private readonly IAudioStorageService _audioStorage;
        private readonly INotificationService _notificationService;

        public AlertsController(
            AppDbContext db,
            IHubContext<AlertHub> hubContext,
            IAudioStorageService audioStorage,
            INotificationService notificationService)
        {
            _db = db;
            _hubContext = hubContext;
            _audioStorage = audioStorage;
            _notificationService = notificationService;
        }

        // POST /api/alerts 
        [HttpPost]
        public async Task<IActionResult> CreateAlert([FromBody] CreateAlertDto dto)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var validTriggers = new[] { "button", "voice", "shake" };
            if (!validTriggers.Contains(dto.TriggerType))
                return BadRequest(new { error = "Invalid trigger type" });

            var alert = new Alert
            {
                ChildId = userId.Value,
                TriggerType = dto.TriggerType,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                AudioUrl = dto.AudioUrl,
                Status = "active",
            };

            _db.Alerts.Add(alert);
            await _db.SaveChangesAsync();

            // Load child info for the response
            var child = await _db.Users.FindAsync(userId.Value);
            if (child == null)
                return NotFound(new { error = "User not found" });

            var alertResponse = new AlertResponseDto
            {
                Id = alert.Id,
                ChildId = alert.ChildId,
                ChildName = child.FullName,
                TriggerType = alert.TriggerType,
                Latitude = alert.Latitude ?? 0,
                Longitude = alert.Longitude ?? 0,
                AudioUrl = alert.AudioUrl,
                Status = alert.Status,
                CreatedAt = alert.CreatedAt,
            };

            // Send real-time alert to all linked parents
            var parentIds = await _db.FamilyLinks
                .Where(fl => fl.ChildId == userId.Value && fl.IsActive)
                .Select(fl => fl.ParentId)
                .ToListAsync();

            // Get parent details for notifications
            var parents = await _db.Users
                .Where(u => parentIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Phone, u.DeviceToken })
                .ToListAsync();

            // Get Egypt time once (outside the loop)
            var egyptTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
            var egyptTime = TimeZoneInfo.ConvertTime(DateTime.Now, egyptTimeZone);

            foreach (var parent in parents)
            {
                // 1. Real-time WebSocket
                await _hubContext.Clients
                    .Group($"parent_{parent.Id}")
                    .SendAsync("ReceiveAlert", alertResponse);

                // 2. Push Notification (Firebase)
                if (!string.IsNullOrEmpty(parent.DeviceToken))
                {
                    await _notificationService.SendPushNotificationAsync(
                        parent.DeviceToken,
                        "🚨 EMERGENCY ALERT!",
                        $"{child.FullName} is in danger at {egyptTime:HH:mm:ss}"
                    );
                }

                // 3. SMS Notification (Twilio)
                if (!string.IsNullOrEmpty(parent.Phone))
                {
                    var smsMessage = $"🚨 EMERGENCY ALERT!\n" +
                                     $"Child: {child.FullName}\n" +
                                     $"Location: https://maps.google.com/maps?q={alertResponse.Latitude},{alertResponse.Longitude}\n" +
                                     $"Time: {egyptTime:HH:mm:ss}\n" +
                                     $"Open SafeGuard app immediately!";

                    await _notificationService.SendSMSAsync(parent.Phone, smsMessage);
                }
            }

            return CreatedAtAction(nameof(GetAlert), new { id = alert.Id }, alertResponse);
        }

        // POST /api/alerts/detect-emergency - Detect emergency keywords and auto-send alert
        [HttpPost("detect-emergency")]
        public async Task<IActionResult> DetectEmergency([FromBody] DetectEmergencyDto dto)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var emergencyKeywords = new[]
            {
                "الحقوني", "ساعديني", "خطر", "مساعدة", "عاجل", "إنقاذ",
                "help", "emergency", "danger", "save me", "urgent", "rescue"
            };

            // Convert text to lowercase for comparison
            var lowerText = dto.Text?.ToLower() ?? "";
            bool isEmergency = emergencyKeywords.Any(keyword => lowerText.Contains(keyword.ToLower()));

            if (!isEmergency)
                return Ok(new { detected = false, message = "No emergency keyword detected" });

            // Emergency detected - Create and send alert automatically
            var child = await _db.Users.FindAsync(userId.Value);
            if (child == null)
                return NotFound(new { error = "User not found" });

            var alert = new Alert
            {
                ChildId = userId.Value,
                TriggerType = "voice",
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Status = "active",
            };

            _db.Alerts.Add(alert);
            await _db.SaveChangesAsync();

            var alertResponse = new AlertResponseDto
            {
                Id = alert.Id,
                ChildId = alert.ChildId,
                ChildName = child.FullName,
                TriggerType = alert.TriggerType,
                Latitude = alert.Latitude ?? 0,
                Longitude = alert.Longitude ?? 0,
                Status = alert.Status,
                CreatedAt = alert.CreatedAt,
            };

            // Send notifications to all linked parents
            var parentIds = await _db.FamilyLinks
                .Where(fl => fl.ChildId == userId.Value && fl.IsActive)
                .Select(fl => fl.ParentId)
                .ToListAsync();

            var parents = await _db.Users
                .Where(u => parentIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Phone, u.DeviceToken })
                .ToListAsync();

            var egyptTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
            var egyptTime = TimeZoneInfo.ConvertTime(DateTime.Now, egyptTimeZone);

            foreach (var parent in parents)
            {
                // Real-time WebSocket
                await _hubContext.Clients
                    .Group($"parent_{parent.Id}")
                    .SendAsync("ReceiveAlert", alertResponse);

                // Push Notification
                if (!string.IsNullOrEmpty(parent.DeviceToken))
                {
                    await _notificationService.SendPushNotificationAsync(
                        parent.DeviceToken,
                        "🚨 EMERGENCY ALERT!",
                        $"{child.FullName} says: {dto.Text}"
                    );
                }

                // SMS Notification
                if (!string.IsNullOrEmpty(parent.Phone))
                {
                    var smsMessage = $"🚨 EMERGENCY ALERT!\n" +
                                     $"Child: {child.FullName}\n" +
                                     $"Said: {dto.Text}\n" +
                                     $"Location: https://maps.google.com/maps?q={alertResponse.Latitude},{alertResponse.Longitude}\n" +
                                     $"Time: {egyptTime:HH:mm:ss}\n" +
                                     $"Open SafeGuard app immediately!";

                    await _notificationService.SendSMSAsync(parent.Phone, smsMessage);
                }
            }

            return Ok(new { detected = true, alert = alertResponse, message = "Emergency alert sent to parents" });
        }

        // POST /api/alerts/upload-audio - Upload audio recording
        [HttpPost("upload-audio")]
        public async Task<IActionResult> UploadAudio(IFormFile audio)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            if (audio == null || audio.Length == 0)
                return BadRequest(new { error = "No audio file provided" });

            var url = await _audioStorage.SaveAudioAsync(audio, userId.Value);

            return Ok(new { url });
        }

        // GET /api/alerts 
        [HttpGet]
        public async Task<IActionResult> GetAlerts()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var user = await _db.Users.FindAsync(userId.Value);
            if (user == null) return Unauthorized();

            List<Alert> alerts;

            if (user.Role == "parent")
            {
                // Get alerts for all linked children
                var childIds = await _db.FamilyLinks
                    .Where(fl => fl.ParentId == userId.Value && fl.IsActive && fl.ChildId != null)
                    .Select(fl => fl.ChildId!.Value)
                    .ToListAsync();

                alerts = await _db.Alerts
                    .Include(a => a.Child)
                    .Where(a => childIds.Contains(a.ChildId))
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();
            }
            else
            {
                // Child sees their own alerts
                alerts = await _db.Alerts
                    .Include(a => a.Child)
                    .Where(a => a.ChildId == userId.Value)
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();
            }

            var response = alerts.Select(a => new AlertResponseDto
            {
                Id = a.Id,
                ChildId = a.ChildId,
                ChildName = a.Child?.FullName ?? "Unknown",
                TriggerType = a.TriggerType,
                Latitude = a.Latitude ?? 0,
                Longitude = a.Longitude ?? 0,
                AudioUrl = a.AudioUrl,
                Status = a.Status,
                CreatedAt = a.CreatedAt,
                AcknowledgedAt = a.AcknowledgedAt,
            });

            return Ok(new { alerts = response });
        }

        // GET /api/alerts/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAlert(Guid id)
        {
            var alert = await _db.Alerts
                .Include(a => a.Child)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (alert == null) return NotFound();

            return Ok(new AlertResponseDto
            {
                Id = alert.Id,
                ChildId = alert.ChildId,
                ChildName = alert.Child?.FullName ?? "Unknown",
                TriggerType = alert.TriggerType,
                Latitude = alert.Latitude ?? 0,
                Longitude = alert.Longitude ?? 0,
                AudioUrl = alert.AudioUrl,
                Status = alert.Status,
                CreatedAt = alert.CreatedAt,
                AcknowledgedAt = alert.AcknowledgedAt,
            });
        }

        // PATCH /api/alerts/{id}/acknowledge 
        [HttpPatch("{id}/acknowledge")]
        public async Task<IActionResult> AcknowledgeAlert(Guid id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var alert = await _db.Alerts.FindAsync(id);
            if (alert == null) return NotFound();

            // Verify parent is linked to this child
            var isLinked = await _db.FamilyLinks
                .AnyAsync(fl => fl.ParentId == userId.Value
                    && fl.ChildId == alert.ChildId
                    && fl.IsActive);

            if (!isLinked)
                return Forbid();

            alert.Status = "acknowledged";
            alert.AcknowledgedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { alert.Id, alert.Status, alert.AcknowledgedAt });
        }

        // PATCH /api/alerts/{id}/resolve
        [HttpPatch("{id}/resolve")]
        public async Task<IActionResult> ResolveAlert(Guid id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var alert = await _db.Alerts.FindAsync(id);
            if (alert == null) return NotFound();

            alert.Status = "resolved";
            await _db.SaveChangesAsync();

            return Ok(new { alert.Id, alert.Status });
        }

        private Guid? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}
