using Elitech.Data;
using Elitech.Models.Alerts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Claims;

namespace Elitech.Controllers;

[ApiController]
[Route("api/elitech/alerts")]
[Authorize]
public class ElitechAlertsNotifyController : ControllerBase
{
    private readonly IMongoCollection<ElitechAlertEvent> _events;

    public ElitechAlertsNotifyController(MongoContext db)
    {
        _events = db.Database.GetCollection<ElitechAlertEvent>("AlertEvents");
    }

    // GET /api/elitech/alerts/unread?take=10
    [HttpGet("unread")]
    public async Task<IActionResult> Unread([FromQuery] int take = 10, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 50);

        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { code = 401, message = "No userId claim" });

        var list = await _events
            .Find(x => x.UserId == userId && !x.IsRead)
            .SortByDescending(x => x.OccurredAtUtc)
            .Limit(take)
            .ToListAsync(ct);

        var dto = list.Select(x => new ElitechAlertEventDto
        {
            Id = x.Id.ToString(),          // ✅ CHÌA KHOÁ
            DeviceGuid = x.DeviceGuid,
            DeviceName = x.DeviceName,
            OccurredAtUtc = x.OccurredAtUtc,
            Reasons = x.Reasons,
            Level = x.Level,
            IsRead = x.IsRead
        });

        return Ok(new { data = dto });
    }

    // POST /api/elitech/alerts/mark-read?id=...
    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkRead([FromQuery] string id, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { code = 401, message = "No userId claim" });

        if (!ObjectId.TryParse(id, out var oid))
            return BadRequest(new { message = "invalid id" });

        var res = await _events.UpdateOneAsync(
            x => x.Id == oid && x.UserId == userId,
            Builders<ElitechAlertEvent>.Update.Set(x => x.IsRead, true),
            cancellationToken: ct);

        return Ok(new { ok = res.ModifiedCount > 0 });
    }

    // POST /api/elitech/alerts/mark-all-read
    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { code = 401, message = "No userId claim" });

        var res = await _events.UpdateManyAsync(
            x => x.UserId == userId && !x.IsRead,
            Builders<ElitechAlertEvent>.Update.Set(x => x.IsRead, true),
            cancellationToken: ct);

        return Ok(new { ok = true, updated = res.ModifiedCount });
    }

    private string? GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        id ??= User.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }
}