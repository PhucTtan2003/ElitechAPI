using Elitech.Services;
using Microsoft.AspNetCore.Mvc;

namespace Elitech.Controllers;

[ApiController]
[Route("api/elitech")]
public class ElitechAlarmController : ControllerBase
{
    private readonly ElitechApiClient _api;
    public ElitechAlarmController(ElitechApiClient api) => _api = api;

    // GET /api/elitech/alarm-record?deviceGuid=...&subUid=0&from=...&to=...&lastHours=24
    [HttpGet("alarm-record")]
    public async Task<IActionResult> GetAlarmRecord(
        [FromQuery] string deviceGuid,
        [FromQuery] int subUid = 0,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int? lastHours = 24,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceGuid))
            return BadRequest("deviceGuid is required");

        var end = to ?? DateTimeOffset.UtcNow;

        if (lastHours.HasValue && lastHours.Value <= 0)
            return BadRequest("lastHours must be > 0");

        var start = from ?? (lastHours.HasValue ? end.AddHours(-lastHours.Value) : end.AddDays(-1));
        if (start > end) (start, end) = (end, start);

        var resp = await _api.GetAlarmRecordAsync(deviceGuid, subUid, start, end, ct);

        if (resp.code != 0)
            return StatusCode(502, new { resp.code, message = resp.message ?? resp.msg, resp.error });

        return Ok(resp);
    }
}
