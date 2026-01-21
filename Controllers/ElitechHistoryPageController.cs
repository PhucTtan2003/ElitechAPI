using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elitech.Controllers
{
    [Authorize]
    public class ElitechHistoryPageController : Controller
    {
        public IActionResult Index()
        {
            ViewBag.IsAdmin = User.IsInRole("Admin");
            return View("Index");
        }
    }
}
