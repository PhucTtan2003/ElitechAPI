using Microsoft.AspNetCore.Mvc;

namespace Elitech.Controllers
{
    public class ElitechAlarmPageController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
