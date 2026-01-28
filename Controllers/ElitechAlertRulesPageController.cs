using System.Security.Claims;
using Elitech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elitech.Controllers;

[Authorize]
[Route("alerts")]
public class ElitechAlertRulesPageController : Controller
{
    private readonly ElitechDeviceAssignmentService _assign;

    public ElitechAlertRulesPageController(ElitechDeviceAssignmentService assign)
    {
        _assign = assign;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return RedirectToAction("Index", "Login");

        var devices = await _assign.GetByUserAsync(userId!, ct);
        ViewBag.Devices = devices;

        return View("~/Views/ElitechAlertRulesPage/Index.cshtml");
    }

    private string? GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        id ??= User.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }
}
