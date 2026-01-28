using System.Security.Claims;
using Elitech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elitech.Controllers;

[Authorize]
[Route("realtime")]
public class ElitechRealtimePageController : Controller
{
    private readonly ElitechDeviceAssignmentService _assign;

    public ElitechRealtimePageController(ElitechDeviceAssignmentService assign)
    {
        _assign = assign;
    }

    [HttpGet]
    [Route("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return RedirectToAction("Index", "Login");

        var set = await _assign.GetDeviceGuidsOfUserAsync(userId!, ct);
        ViewBag.DeviceGuids = string.Join(",", set);

        return View("~/Views/ElitechRealtime/Index.cshtml");
    }

    private string? GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        id ??= User.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }
}
