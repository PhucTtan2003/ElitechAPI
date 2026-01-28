// Controllers/ElitechDeviceController.cs
using Elitech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/elitech")]
public class ElitechDeviceController : ControllerBase
{           
    private readonly Elitech.Services.ElitechApiClient _api;
    private readonly ElitechDeviceAssignmentService _assign;
    public ElitechDeviceController(ElitechApiClient api, ElitechDeviceAssignmentService assign)
    {
        _api = api;
        _assign = assign;
    }

    [HttpGet("device-info")]
    public async Task<IActionResult> GetDeviceInfo([FromQuery] string deviceGuids, CancellationToken ct)
    {
        var list = (deviceGuids ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (list.Length == 0)
            return BadRequest(new { code = 400, message = "deviceGuids required (comma separated)" });

        try
        {
            var resp = await _api.GetDeviceInfoAsync(list, ct);
            if (resp?.code is null or not 0)
                return StatusCode(502, new { code = resp?.code ?? -1, message = resp?.msg ?? resp?.message ?? "Upstream error" });

            long ToSec(long? v) => v is null ? 0 : (v > 3_000_000_000 ? v.Value / 1000 : v.Value);

            var data = resp!.data?.Select(x => new {
                deviceName = x.deviceName,
                deviceGuid = x.deviceGuid,
                deviceTypeName = x.deviceTypeName,
                sceneName = x.sceneName,
                subUid = x.subUid,
                smsCount = x.smsCount,
                voiceCount = x.voiceCount,
                expiredTime = x.expiredTime,
                lastTime = x.lastTime,
                lastTimeStr = x.lastTime.HasValue ?
                    DateTimeOffset.FromUnixTimeSeconds(x.lastTime.Value).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : null
            });

            return Ok(new { code = 0, data });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { code = 502, message = "Upstream HTTP error", detail = ex.Message });
        }
        catch (System.Text.Json.JsonException ex)
        {
            return StatusCode(500, new { code = 5001, message = "JSON parse error", detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = 5000, message = ex.Message });
        }
    }
    // =========================
    // API #7 - Get Device GUIDs visible to the user (Elitech)
    // GET /api/elitech/device-guids
    // =========================
    [HttpGet("device-guids")]
    public async Task<IActionResult> GetDeviceGuids(CancellationToken ct)
    {
        try
        {
            // Bạn cần thêm method _api.GetDeviceGuidsAsync(ct)
            var resp = await _api.GetDeviceGuidsAsync(ct);

            if (resp is null)
                return StatusCode(502, new { code = -1, message = "Empty upstream response" });

            if (resp.Code != 0)
                return StatusCode(502, new { code = resp.Code, message = resp.Msg ?? resp.Message ?? "Upstream error", resp.Error });

            return Ok(new { code = 0, data = resp.Data ?? new List<string>() });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { code = 502, message = "Upstream HTTP error", detail = ex.Message });
        }
        catch (System.Text.Json.JsonException ex)
        {
            return StatusCode(500, new { code = 5001, message = "JSON parse error", detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = 5000, message = ex.Message });
        }
    }
}