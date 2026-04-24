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
}
