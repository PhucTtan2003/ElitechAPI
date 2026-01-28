// Controllers/ElitechDevicePageController.cs
using Microsoft.AspNetCore.Mvc;

public class ElitechDevicePageController : Controller
{
    public IActionResult Index([FromQuery] string? deviceGuids = null)
    {
        ViewBag.DeviceGuids = deviceGuids ?? "";
        return View(); // Views/ElitechDevice/Index.cshtml
    }
}