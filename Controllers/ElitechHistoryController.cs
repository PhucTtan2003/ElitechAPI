using Elitech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Elitech.Controllers;

[ApiController]
[Route("api/elitech")]
[Authorize]
public class ElitechHistoryController : ControllerBase
{
    private readonly ElitechApiClient _api;
    private readonly ElitechDeviceAssignmentService _assign;
    private readonly IMemoryCache _cache;

    public ElitechHistoryController(ElitechApiClient api, ElitechDeviceAssignmentService assign, IMemoryCache cache)
    {
        _api = api;
        _assign = assign;
        _cache = cache;
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string deviceGuid,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? lastHours,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deviceGuid))
            return BadRequest(new { code = 400, message = "Thiếu deviceGuid." });

        deviceGuid = deviceGuid.Trim();

        // ✅ auth (giữ nguyên như bạn đang làm)
        string? userId = null;
        if (!User.IsInRole("Admin"))
        {
            userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { code = 401, message = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại." });

            var ok = await _assign.IsAssignedAsync(userId, deviceGuid, ct);
            if (!ok) return Forbid();
        }
        else
        {
            userId = GetUserId() ?? "admin";
        }

        var end = to ?? DateTimeOffset.UtcNow;
        if (lastHours.HasValue && lastHours.Value <= 0)
            return BadRequest(new { code = 400, message = "lastHours phải > 0." });

        var start = from ?? (lastHours.HasValue ? end.AddHours(-lastHours.Value) : end.AddDays(-1));
        if (start > end) (start, end) = (end, start);

        // ✅ Round theo phút để tăng cache hit (tuỳ bạn có muốn hay không)
        start = RoundToMinute(start);
        end = RoundToMinute(end);

        // ✅ Cache key theo user/role + device + range
        var role = User.IsInRole("Admin") ? "Admin" : "User";
        var cacheKey = $"history:{role}:{userId}:{deviceGuid}:{start:O}:{end:O}";

        // ✅ 1) Hit cache → trả luôn
        if (_cache.TryGetValue(cacheKey, out object? cached) && cached is not null)
            return Ok(cached);

        // ✅ 2) Miss → gọi upstream
        var resp = await _api.GetHistoryDataRangeAsync(deviceGuid, start, end, ct, 150);

        if (resp.code != 0)
        {
            // không cache lỗi (tránh “dính” lỗi)
            return StatusCode(502, new { code = 502, message = "Elitech upstream đang lỗi, vui lòng thử lại." });
        }

        // ✅ 3) Cache kết quả thành công
        // TTL gợi ý: 20-30s (tuỳ UX bạn)
        _cache.Set(cacheKey, resp, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(25),
            SlidingExpiration = TimeSpan.FromSeconds(10),
            Size = 1 // nếu bạn bật SizeLimit (optional)
        });

        return Ok(resp);
    }

    private static DateTimeOffset RoundToMinute(DateTimeOffset dt)
        => new DateTimeOffset(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, dt.Offset);

    private string? GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        id ??= User.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }
}