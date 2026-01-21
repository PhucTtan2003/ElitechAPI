using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Elitech.Services;

namespace Elitech.Controllers;

[Authorize] // tùy bạn, có thể bỏ nếu muốn public
public class ElitechRealtimeController : Controller
{
    private readonly ElitechApiClient _api;

    public ElitechRealtimeController(ElitechApiClient api)
    {
        _api = api;
    }

    // Trang UI hiển thị realtime
    // /ElitechRealtime?deviceGuids=GUID1,GUID2
    [HttpGet]
    public IActionResult Index(string? deviceGuids = null)
    {
        ViewBag.DeviceGuids = deviceGuids ?? "";
        return View();
    }

    // API trả JSON cho frontend poll
    // GET /api/elitech/realtime?deviceGuids=GUID1,GUID2
    [HttpGet("/api/elitech/realtime")]
    public async Task<IActionResult> GetRealtime([FromQuery] string deviceGuids, CancellationToken ct)
    {
        var list = (deviceGuids ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (list.Length == 0)
            return BadRequest(new { message = "deviceGuids is required (comma separated)" });

        var data = await _api.GetRealTimeDataAsync(list, ct);
        return Ok(data);
    }
}
