using System.Security.Claims;
using Elitech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elitech.Controllers;

[ApiController]
[Route("api/elitech")]
[Authorize] // ✅ bắt buộc login
public class ElitechHistoryController : ControllerBase
{
    private readonly ElitechApiClient _api;
    private readonly ElitechDeviceAssignmentService _assign;

    public ElitechHistoryController(ElitechApiClient api, ElitechDeviceAssignmentService assign)
    {
        _api = api;
        _assign = assign;
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
            return BadRequest("deviceGuid is required");

        deviceGuid = deviceGuid.Trim();

        // ✅ User chỉ xem được device đã gán, Admin bỏ qua
        if (!User.IsInRole("Admin"))
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { code = 401, message = "No userId claim" });

            var ok = await _assign.IsAssignedAsync(userId, deviceGuid, ct);
            if (!ok)
                return Forbid(); // 403
        }

        var end = to ?? DateTimeOffset.UtcNow;

        if (lastHours.HasValue && lastHours.Value <= 0)
            return BadRequest("lastHours must be > 0");

        var start = from ?? (lastHours.HasValue ? end.AddHours(-lastHours.Value) : end.AddDays(-1));
        if (start > end) (start, end) = (end, start);

        var resp = await _api.GetHistoryDataRangeAsync(deviceGuid, start, end, ct, 150);

        if (resp.code != 0)
            return StatusCode(502, new { resp.code, message = resp.message ?? resp.msg, resp.error });

        return Ok(resp);
    }

    // =========================
    // Helpers
    // =========================
    private string? GetUserId()
    {
        // ưu tiên ClaimTypes.NameIdentifier
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // fallback: JWT phổ biến
        id ??= User.FindFirstValue("sub");

        // nếu bạn đang dùng claim khác, thêm ở đây:
        // id ??= User.FindFirstValue("uid");

        return string.IsNullOrWhiteSpace(id) ? null : id;
    }
}
