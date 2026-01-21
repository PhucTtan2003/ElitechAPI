using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elitech.Controllers
{
    [Route("api")]
    public class ApiController : Controller
    {
        [HttpGet("me")]
        public IActionResult Me()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Json(new { name = (string?)null, role = (string?)null });

            var name = User.Identity!.Name ?? "";
            var role = User.IsInRole("Admin") ? "Admin" : (User.IsInRole("User") ? "User" : "");
            return Json(new { name, role });
        }
    }
}