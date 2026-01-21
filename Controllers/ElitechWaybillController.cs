using Elitech.Models.Waybill;
using Elitech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static Elitech.Models.Waybill.BatchWaybillViewModel;

namespace Elitech.Controllers;

[Authorize] // nếu muốn chặt: [Authorize(Roles="Admin")]
public class ElitechWaybillController : Controller
{
    private readonly ElitechApiClient _api;
    public ElitechWaybillController(ElitechApiClient api) => _api = api;

    // UI: /ElitechWaybill
    [HttpGet]
    public IActionResult Index()
    {
        return View(); // Views/ElitechWaybill/Index.cshtml
    }

    // JSON API: POST /api/elitech/batch-waybill
    [HttpPost("/api/elitech/batch-waybill")]
    public async Task<IActionResult> BatchWaybill([FromBody] BatchWaybillReq req, CancellationToken ct)
    {
        var list = (req.deviceGuids ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (list.Count == 0)
            return BadRequest(new { code = 400, message = "deviceGuids required" });

        if (list.Count > 200)
            return BadRequest(new { code = 400, message = "deviceGuids max 200" });

        if (req.waybillStartTime is null && req.waybillStopTime is null)
            return BadRequest(new { code = 400, message = "waybillStartTime or waybillStopTime is required" });

        var resp = await _api.BatchSetWaybillAsync(list, req.waybillStartTime, req.waybillStopTime, ct);

        if (resp.code != 0)
            return StatusCode(502, resp);

        return Ok(resp);
    }
}
