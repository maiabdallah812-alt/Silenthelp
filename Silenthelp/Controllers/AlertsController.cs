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
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AlertsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<AlertHub> _hubContext;
        private readonly IAudioStorageService _audioStorage;

        public AlertsController(
            AppDbContext db,
            IHubContext<AlertHub> hubContext,
            IAudioStorageService audioStorage)
        {
            _db = db;
            _hubContext = hubContext;
            _audioStorage = audioStorage;
        }

        // POST /api/alerts - Child sends emergency alert
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

            var alertResponse = new AlertResponseDto
            {
                Id = alert.Id,
                ChildId = alert.ChildId,
                ChildName = child?.FullName ?? "Unknown",
                TriggerType = alert.TriggerType,
                Latitude = alert.Latitude,
                Longitude = alert.Longitude,
                AudioUrl = alert.AudioUrl,
                Status = alert.Status,
                CreatedAt = alert.CreatedAt,
            };

            // Send real-time alert to all linked parents
            var parentIds = await _db.FamilyLinks
                .Where(fl => fl.ChildId == userId.Value && fl.IsActive)
                .Select(fl => fl.ParentId)
                .ToListAsync();

            foreach (var parentId in parentIds)
            {
                await _hubContext.Clients
                    .Group($"parent_{parentId}")
                    .SendAsync("ReceiveAlert", alertResponse);
            }

            return CreatedAtAction(nameof(GetAlert), new { id = alert.Id }, alertResponse);
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

        // GET /api/alerts - Parent gets all alerts for their children
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
                Latitude = a.Latitude,
                Longitude = a.Longitude,
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
                Latitude = alert.Latitude,
                Longitude = alert.Longitude,
                AudioUrl = alert.AudioUrl,
                Status = alert.Status,
                CreatedAt = alert.CreatedAt,
                AcknowledgedAt = alert.AcknowledgedAt,
            });
        }

        // PATCH /api/alerts/{id}/acknowledge - Parent acknowledges alert
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
