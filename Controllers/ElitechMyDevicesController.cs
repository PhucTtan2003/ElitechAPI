using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Elitech.Services;
using Elitech.Models;

namespace Elitech.Controllers;

[ApiController]
[Route("api/elitech")]
[Authorize]
public class ElitechMyDevicesController : ControllerBase
{
    private readonly ElitechDeviceAssignmentService _assign;

    public ElitechMyDevicesController(ElitechDeviceAssignmentService assign)
    {
        _assign = assign;
    }

    // USER: chỉ thấy thiết bị được gán cho chính mình
    [Authorize]
    [HttpGet("my-devices")]
    public async Task<IActionResult> MyDevices(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { code = 401, message = "Missing NameIdentifier claim" });

        try
        {
            var list = await _assign.GetByUserAsync(userId, ct) ?? new List<ElitechDeviceAssignment>();

            var data = list
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.DeviceGuid))
                .Select(x => new { deviceGuid = x.DeviceGuid, deviceName = x.DeviceName });

            return Ok(new { code = 0, data });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = 5000, message = "my-devices failed", detail = ex.Message });
        }
    }

    // ADMIN: thấy toàn bộ thiết bị đã được gán trong DB (distinct theo DeviceGuid)
    [Authorize(Roles = "Admin")]
    [HttpGet("all-devices")]
    public async Task<IActionResult> AllDevices(CancellationToken ct)
    {
        try
        {
            var list = await _assign.GetAllDevicesAsync(ct);

            var data = list
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.DeviceGuid))
                .Select(x => new { deviceGuid = x.DeviceGuid, deviceName = x.DeviceName });

            return Ok(new { code = 0, data });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = 5001, message = "all-devices failed", detail = ex.Message });
        }
    }

}
