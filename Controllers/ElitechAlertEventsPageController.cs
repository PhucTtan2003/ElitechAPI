using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elitech.Controllers;

[Authorize]
[Route("alert-events")]
public class ElitechAlertEventsPageController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View("~/Views/ElitechAlertEventsPage/Index.cshtml");
    }
}
