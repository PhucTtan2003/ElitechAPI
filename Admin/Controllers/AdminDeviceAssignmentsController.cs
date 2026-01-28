using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Elitech.Services;

namespace Elitech.Controllers;

[ApiController]
[Route("api/admin/device-assignments")]
[Authorize(Roles = "Admin")]
public class AdminDeviceAssignmentsController : ControllerBase
{
    private readonly ElitechDeviceAssignmentService _assign;

    public AdminDeviceAssignmentsController(ElitechDeviceAssignmentService assign)
    {
        _assign = assign;
    }

    public record AssignReq(string UserId, string DeviceGuid, string? DeviceName);

    [HttpPost("assign")]
    public async Task<IActionResult> Assign([FromBody] AssignReq req, CancellationToken ct)
    {
        await _assign.AssignAsync(req.UserId, req.DeviceGuid, req.DeviceName, ct);
        return Ok(new { code = 0 });
    }

    [HttpDelete("unassign")]
    public async Task<IActionResult> Unassign([FromQuery] string userId, [FromQuery] string deviceGuid, CancellationToken ct)
    {
        var removed = await _assign.UnassignAsync(userId, deviceGuid, ct);
        return Ok(new { code = 0, removed });
    }

    [HttpGet("by-user")]
    public async Task<IActionResult> ByUser([FromQuery] string userId, CancellationToken ct)
    {
        var list = await _assign.GetByUserAsync(userId, ct);
        return Ok(new { code = 0, data = list.Select(x => new { x.UserId, x.DeviceGuid, x.DeviceName, x.AssignedAtUtc }) });
    }
}
