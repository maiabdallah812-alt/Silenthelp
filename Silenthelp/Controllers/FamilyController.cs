using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SilentHelp.Data;
using SilentHelp.Models;
using SilentHelp.Models.DTOs;

namespace Silenthelp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FamilyController : ControllerBase
    {
        private readonly AppDbContext _db;

        public FamilyController(AppDbContext db)
        {
            _db = db;
        }

        // POST /api/family/generate-invite - Parent generates invite code
        [HttpPost("generate-invite")]
        public async Task<IActionResult> GenerateInvite()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var link = new FamilyLink
            {
                ParentId = userId.Value,
                ChildId = null, // Will be set when child joins
            };

            _db.FamilyLinks.Add(link);
            await _db.SaveChangesAsync();

            return Ok(new { invite_code = link.InviteCode });
        }

        // POST /api/family/join - Child joins family with invite code
        [HttpPost("join")]
        public async Task<IActionResult> JoinFamily([FromBody] JoinFamilyDto dto)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.InviteCode))
                return BadRequest(new { error = "Invite code is required" });

            var link = await _db.FamilyLinks
                .FirstOrDefaultAsync(fl =>
                    fl.InviteCode == dto.InviteCode
                    && fl.ChildId == null
                    && fl.IsActive);

            if (link == null)
                return NotFound(new { error = "Invalid or expired invite code" });

            // Prevent parent from linking to themselves
            if (link.ParentId == userId.Value)
                return BadRequest(new { error = "Cannot link to yourself" });

            link.ChildId = userId.Value;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Successfully joined family!" });
        }

        // GET /api/family/members - Get linked family members
        [HttpGet("members")]
        public async Task<IActionResult> GetMembers()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var user = await _db.Users.FindAsync(userId.Value);
            if (user == null) return Unauthorized();

            List<FamilyMemberDto> members;

            if (user.Role == "parent")
            {
                // Get linked children
                members = await _db.FamilyLinks
                    .Where(fl => fl.ParentId == userId.Value && fl.ChildId != null && fl.IsActive)
                    .Select(fl => new FamilyMemberDto
                    {
                        Id = fl.Child!.Id,
                        FullName = fl.Child.FullName,
                        Role = fl.Child.Role,
                    })
                    .ToListAsync();
            }
            else
            {
                // Get linked parents
                members = await _db.FamilyLinks
                    .Where(fl => fl.ChildId == userId.Value && fl.IsActive)
                    .Select(fl => new FamilyMemberDto
                    {
                        Id = fl.Parent.Id,
                        FullName = fl.Parent.FullName,
                        Role = fl.Parent.Role,
                    })
                    .ToListAsync();
            }

            return Ok(new { members });
        }

        // DELETE /api/family/unlink/{linkId}
        [HttpDelete("unlink/{linkId}")]
        public async Task<IActionResult> Unlink(Guid linkId)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var link = await _db.FamilyLinks.FindAsync(linkId);
            if (link == null) return NotFound();

            if (link.ParentId != userId.Value && link.ChildId != userId.Value)
                return Forbid();

            link.IsActive = false;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Family link removed" });
        }

        private Guid? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}
