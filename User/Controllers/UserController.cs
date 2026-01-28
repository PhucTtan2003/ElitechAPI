using Elitech.Models.ViewModels;
using Elitech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Elitech.Controllers
{
    [Authorize(Roles = "User")]
    public class UserController : Controller
    {
        private readonly ElitechDeviceAssignmentService _assign;

        public UserController(ElitechDeviceAssignmentService assign)
        {
            _assign = assign;
        }

        // GET /user?deviceGuid=...
        [HttpGet("/user")]
        public async Task<IActionResult> Index(string? deviceGuid, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var assigned = await _assign.GetByUserAsync(userId, ct);

            var devices = assigned
                .Select(x => new UserDeviceVm
                {
                    DeviceGuid = x.DeviceGuid,
                    DeviceName = string.IsNullOrWhiteSpace(x.DeviceName) ? null : x.DeviceName
                })
                .OrderBy(x => x.DeviceName ?? x.DeviceGuid)
                .ToList();

            var selected = (deviceGuid ?? "").Trim();
            if (string.IsNullOrWhiteSpace(selected) && devices.Count > 0)
                selected = devices[0].DeviceGuid;

            if (!string.IsNullOrWhiteSpace(selected) &&
                devices.Count > 0 &&
                !devices.Any(d => string.Equals(d.DeviceGuid, selected, StringComparison.OrdinalIgnoreCase)))
            {
                selected = devices[0].DeviceGuid;
            }

            var vm = new UserDashboardVm
            {
                Devices = devices,
                SelectedDeviceGuid = selected
            };

            return View(vm); // Views/User/Index.cshtml
        }
    }
}