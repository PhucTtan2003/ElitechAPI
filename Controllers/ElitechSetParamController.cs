using Elitech.Models.SetParam;
using Elitech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elitech.Controllers;

[Authorize]
public class ElitechSetParamController : Controller
{
    private readonly ElitechApiClient _api;
    public ElitechSetParamController(ElitechApiClient api) => _api = api;

    // UI: /ElitechSetParam?deviceGuid=...
    [HttpGet]
    public IActionResult Index(string? deviceGuid = null)
    {
        ViewBag.DeviceGuid = deviceGuid ?? "";
        return View(); // Views/ElitechSetParam/Index.cshtml
    }

    // JSON API: POST /api/elitech/set-param
    [HttpPost("/api/elitech/set-param")]
    public async Task<IActionResult> SetParam([FromBody] SetParamReq req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.deviceGuid))
            return BadRequest(new { code = 400, message = "deviceGuid is required" });

        var resp = await _api.SetParamAsync(req, ct);

        // Trả phản hồi debug
        if (resp.code != 0)
            return StatusCode(502, resp);

        return Ok(resp);
    }
}