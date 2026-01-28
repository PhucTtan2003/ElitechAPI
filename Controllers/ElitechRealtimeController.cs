using System.Security.Claims;
using Elitech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elitech.Controllers;

[ApiController]
[Route("api/elitech")]
[Authorize]
public class ElitechRealtimeController : ControllerBase
{
    private readonly ElitechRealtimeCacheService _cache;
    private readonly ElitechDeviceAssignmentService _assign;

    public ElitechRealtimeController(
        ElitechRealtimeCacheService cache,
        ElitechDeviceAssignmentService assign)
    {
        _cache = cache;
        _assign = assign;
    }

    // GET /api/elitech/realtime?deviceGuids=...
    // hoặc GET /api/elitech/realtime (auto lấy theo user)
    [HttpGet("realtime")]
    public async Task<IActionResult> GetRealtime(
        [FromQuery] string? deviceGuids,
        [FromQuery] int? maxAgeSeconds, // optional: FE muốn dữ liệu không quá cũ
        CancellationToken ct)
    {
        string[] list;

        if (string.IsNullOrWhiteSpace(deviceGuids))
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var set = await _assign.GetDeviceGuidsOfUserAsync(userId!, ct);
            list = set.ToArray();
        }
        else
        {
            list = deviceGuids
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (!User.IsInRole("Admin"))
            {
                var userId = GetUserId();
                if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

                var allowed = await _assign.GetDeviceGuidsOfUserAsync(userId!, ct);
                if (list.Any(x => !allowed.Contains(x)))
                    return Forbid();
            }
        }

        if (list.Length == 0)
            return Ok(new { data = Array.Empty<object>(), meta = new { cached = true, count = 0 } });

        var maxAge = TimeSpan.FromSeconds(Math.Clamp(maxAgeSeconds ?? 600, 10, 3600)); // default 10 phút
        var rows = _cache.GetMany(list, maxAge);

        return Ok(new
        {
            data = rows,
            meta = new
            {
                cached = true,
                cacheCount = _cache.Count,
                requested = list.Length,
                returned = rows.Count,
                maxAgeSeconds = (int)maxAge.TotalSeconds
            }
        });
    }

    private string? GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        id ??= User.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }
}
