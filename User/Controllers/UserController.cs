using Elitech.Services;
using Elitech.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Elitech.Controllers
{
    [Authorize(Roles = "User")]
    [Route("api/elitech/user")]
    public class UserController : Controller
    {
        private readonly ElitechDeviceAssignmentService _assign;
        private readonly IHubContext<ElitechAlarmHub> _hub;

        public UserController(ElitechDeviceAssignmentService assign, IHubContext<ElitechAlarmHub> hub)
        {
            _assign = assign;
            _hub = hub;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "";
            var devices = string.IsNullOrWhiteSpace(userId)
                ? new List<object>()
                : (await _assign.GetByUserAsync(userId, ct))
                    .Select(x => new { x.DeviceGuid, x.DeviceName })
                    .ToList<object>();

            ViewBag.Devices = devices;
            return View();
        }
    }
}
