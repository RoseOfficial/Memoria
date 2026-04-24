using AlphaScopeServer.Data;
using AlphaScopeServer.Models.DTOs;
using AlphaScopeServer.Models.Entities;
using AlphaScopeServer.Services.Takedowns;
using AlphaScopeServer.Services.World;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlphaScopeServer.Controllers;

[ApiController]
[Route("v1/[controller]")]
public class TakedownsController : ControllerBase
{
    private readonly AlphaScopeDbContext _context;
    private readonly TakedownRateLimiter _rateLimiter;
    private readonly ILogger<TakedownsController> _logger;

    public TakedownsController(AlphaScopeDbContext context, TakedownRateLimiter rateLimiter, ILogger<TakedownsController> logger)
    {
        _context = context;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Submit([FromBody] TakedownSubmitRequest body)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var ipHash = TakedownRateLimiter.HashIp(ip);
        if (!_rateLimiter.Allow(ipHash)) return StatusCode(429);

        var worldId = WorldNames.ResolveFromSlug(body.WorldSlug);
        long? resolvedId = null;
        if (worldId.HasValue)
        {
            var candidates = await _context.Players
                .Where(p => p.HomeWorldId == worldId)
                .ToListAsync();
            var match = candidates.FirstOrDefault(p => WorldNames.ToSlug(p.Name) == body.NameSlug.ToLowerInvariant());
            if (match != null) resolvedId = match.LocalContentId;
        }

        var row = new TakedownRequest
        {
            WorldSlug = body.WorldSlug.ToLowerInvariant(),
            NameSlug = body.NameSlug.ToLowerInvariant(),
            ResolvedPlayerLocalContentId = resolvedId,
            Reason = body.Reason,
            ContactEmail = body.ContactEmail,
            SubmitterIpHash = ipHash,
        };
        _context.TakedownRequests.Add(row);
        await _context.SaveChangesAsync();

        _logger.LogWarning("Takedown submitted: {WorldSlug}/{NameSlug} by {IpHash}", row.WorldSlug, row.NameSlug, row.SubmitterIpHash);

        return Accepted();
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string status = "pending")
    {
        var isAdmin = (bool)(HttpContext.Items["IsAdmin"] ?? false);
        if (!isAdmin) return NotFound();

        if (!Enum.TryParse<TakedownStatus>(status, ignoreCase: true, out var parsed))
            return BadRequest("invalid status");

        var items = await _context.TakedownRequests
            .Where(t => t.Status == parsed)
            .OrderBy(t => t.SubmittedAt)
            .Select(t => new TakedownListItem(
                t.Id, t.WorldSlug, t.NameSlug, t.ResolvedPlayerLocalContentId,
                t.Reason, t.ContactEmail, t.SubmittedAt, t.Status.ToString()))
            .ToListAsync();

        return Ok(items);
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Act(int id, [FromBody] TakedownActionRequest body)
    {
        var isAdmin = (bool)(HttpContext.Items["IsAdmin"] ?? false);
        var viewerUserId = HttpContext.Items["ViewerUserId"] as int?;
        if (!isAdmin || viewerUserId is null) return NotFound();

        var row = await _context.TakedownRequests.FindAsync(id);
        if (row is null) return NotFound();
        if (row.Status != TakedownStatus.Pending) return Conflict("already resolved");

        row.ResolvedByUserId = viewerUserId.Value;
        row.ResolvedAt = DateTime.UtcNow;
        row.ResolutionNotes = body.Notes;

        switch (body.Action?.ToLowerInvariant())
        {
            case "approve":
                row.Status = TakedownStatus.Resolved;
                if (row.ResolvedPlayerLocalContentId is long pid)
                {
                    var player = await _context.Players.FindAsync(pid);
                    if (player != null) player.HideEntirely = true;
                }
                break;
            case "reject":
                row.Status = TakedownStatus.Rejected;
                break;
            default:
                return BadRequest("action must be 'approve' or 'reject'");
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }
}
