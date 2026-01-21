// Controllers/ElitechDevicePageController.cs
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Elitech.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elitech.Controllers
{
    [Authorize]
    [Route("ElitechDevicePage")]
    public class ElitechDevicePageController : Controller
    {
        private readonly ElitechDeviceAssignmentService _assign;

        public ElitechDevicePageController(ElitechDeviceAssignmentService assign)
        {
            _assign = assign;
        }

        // GET /ElitechDevicePage
        // deviceGuids query chỉ là optional (ưu tiên lấy từ DB theo user)
        [HttpGet("")]
        public async Task<IActionResult> Index([FromQuery] string? deviceGuids = null, CancellationToken ct = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToAction("Index", "Login");

            // 1) Lấy danh sách thiết bị đã gán cho user từ DB
            var devices = await _assign.GetByUserAsync(userId, ct);
            devices ??= new();

            // 2) Nếu bạn vẫn muốn cho phép override bằng query string:
            // - có deviceGuids trên URL thì dùng nó
            // - không có thì build deviceGuids từ danh sách gán
            var guidsFromDb = devices
                .Select(d => d.DeviceGuid ?? d.DeviceGuid) // tuỳ model bạn đặt field
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            var finalGuids = !string.IsNullOrWhiteSpace(deviceGuids)
                ? deviceGuids
                : string.Join(",", guidsFromDb);

            // 3) Đổ dữ liệu ra view để UI render
            ViewBag.Devices = devices;              // quan trọng: user sẽ thấy thiết bị
            ViewBag.DeviceGuids = finalGuids ?? ""; // nếu bạn dùng chuỗi guids

            // NOTE: đúng path view của bạn. Bạn đang comment "Views/ElitechDevice/Index.cshtml"
            // Nếu view thật là Views/ElitechDevicePage/Index.cshtml thì đổi lại.
            return View("~/Views/ElitechDevicePage/Index.cshtml");
        }
    }
}
