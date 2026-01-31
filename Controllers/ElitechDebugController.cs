using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Elitech.Hubs;

namespace Elitech.Controllers;

[ApiController]
[Route("api/debug")]
[Authorize] // phải login mới test đúng group user
public class ElitechDebugController : ControllerBase
{
    private readonly IHubContext<ElitechAlarmHub> _hub;

    public ElitechDebugController(IHubContext<ElitechAlarmHub> hub)
    {
        _hub = hub;
    }

    [HttpPost("toast")]
    public async Task<IActionResult> PushToast([FromQuery] string level = "high")
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
        var name = User.Identity?.Name ?? "";

        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("Missing userId claim");

        var payload = new
        {
            deviceGuid = "TEST-DEV-001",
            deviceName = "Test Device",
            level = level, // high|warn|ok
            title = "TEST PUSH",
            message = $"Hello {name} (role={role}, userId={userId}) • time={DateTime.Now:HH:mm:ss}",
            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // ✅ bắn thẳng vào group user:{userId}
        await _hub.Clients.Group($"user:{userId}")
            .SendAsync("alert.toast", payload);

        return Ok(new { ok = true, sentTo = $"user:{userId}", payload });
    }

    [HttpPost("toast-role")]
    public async Task<IActionResult> PushToastToRole([FromQuery] string level = "warn")
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
        if (string.IsNullOrWhiteSpace(role))
            return BadRequest("Missing role claim");

        var payload = new
        {
            level,
            title = "TEST ROLE PUSH",
            message = $"Role={role} • {DateTime.Now:HH:mm:ss}",
            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _hub.Clients.Group($"role:{role}")
            .SendAsync("alert.toast", payload);

        return Ok(new { ok = true, sentTo = $"role:{role}", payload });
    }
}
