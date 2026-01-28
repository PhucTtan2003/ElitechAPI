using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Elitech.Data;
using Elitech.Models;

using Elitech.Services;

namespace Elitech.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly AccountService _accounts;
        private readonly ElitechDeviceAssignmentService _assign;

        // from ElitechAdminController
        private readonly ElitechApiClient _api;
        private readonly MongoContext _db;
        private readonly ILogger<AdminController> _logger;
        private readonly IWebHostEnvironment _env;

        public AdminController(
            AccountService accounts,
            ElitechDeviceAssignmentService assign,
            ElitechApiClient api,
            MongoContext db,
            ILogger<AdminController> logger,
            IWebHostEnvironment env)
        {
            _accounts = accounts;
            _assign = assign;

            _api = api;
            _db = db;
            _logger = logger;
            _env = env;
        }

        // =========================
        // VIEWS
        // =========================
        [HttpGet("")]
        public IActionResult Index() => View();

        [HttpGet("AssignDevices")]
        public IActionResult AssignDevices() => View();

        // =========================
        // ADMIN JSON API (under /Admin)
        // =========================
        [HttpGet("GetUsers")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _accounts.GetByRoleAsync("User");
            var data = users.Select(u => new { userId = u.Id, username = u.Username });
            return Json(new { code = 0, data });
        }

        [HttpGet("GetAssignments")]
        public async Task<IActionResult> GetAssignments([FromQuery] string userId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return Json(new { code = 1, message = "userId required" });

            var list = await _assign.GetByUserAsync(userId, ct);
            var data = list.Select(x => new
            {
                deviceGuid = x.DeviceGuid,
                deviceName = x.DeviceName,
                assignedAtUtc = x.AssignedAtUtc
            });

            return Json(new { code = 0, data });
        }

        [HttpPost("AssignDevices")]
        public async Task<IActionResult> AssignDevice([FromBody] AssignReq? req, CancellationToken ct)
        {
            if (req is null)
                return BadRequest(new { code = 1, message = "Body required (application/json)" });

            if (string.IsNullOrWhiteSpace(req.UserId) || string.IsNullOrWhiteSpace(req.DeviceGuid))
                return BadRequest(new { code = 1, message = "Invalid payload" });

            await _assign.AssignAsync(req.UserId, req.DeviceGuid, req.DeviceName, ct);
            return Ok(new { code = 0 });
        }

        [HttpPost("UnassignDevice")]
        public async Task<IActionResult> UnassignDevice([FromBody] AssignReq? req, CancellationToken ct)
        {
            if (req is null)
                return BadRequest(new { code = 1, message = "Body required (application/json)" });

            if (string.IsNullOrWhiteSpace(req.UserId) || string.IsNullOrWhiteSpace(req.DeviceGuid))
                return BadRequest(new { code = 1, message = "Invalid payload" });

            await _assign.UnassignAsync(req.UserId, req.DeviceGuid, ct);
            return Ok(new { code = 0 });
        }

        public record AssignReq(string UserId, string DeviceGuid, string? DeviceName);

        // =========================
        // UPSTREAM ELITECH API #8 (absolute route, not under /Admin)
        // POST /api/admin/elitech/add-device
        // =========================

        public class AddDeviceReq
        {
            public string DeviceGuid { get; set; } = "";
            public string DeviceName { get; set; } = "";
        }

        /// <summary>
        /// API #8 - Bind device vào Elitech API account
        /// </summary>
        [HttpPost("/api/admin/elitech/add-device")]
        public async Task<IActionResult> AddDevice([FromBody] AddDeviceReq req, CancellationToken ct)
        {
            var errorId = Guid.NewGuid().ToString("N");

            try
            {
                if (req == null)
                    return BadRequest(new { code = 400, message = "Request body is required" });

                if (string.IsNullOrWhiteSpace(req.DeviceGuid))
                    return BadRequest(new { code = 400, message = "deviceGuid is required" });

                var deviceGuid = req.DeviceGuid.Trim();
                var deviceName = string.IsNullOrWhiteSpace(req.DeviceName)
                    ? $"Device {deviceGuid}"
                    : req.DeviceName.Trim();

                // 1) Call Elitech API #8 (addDevice)
                AddDeviceResp? resp;
                try
                {
                    resp = await _api.AddDeviceAsync(new[]
                    {
                        new AddDeviceInfo(deviceName, deviceGuid)
                    }, ct);
                }
                catch (Exception exUp)
                {
                    _logger.LogError(exUp,
                        "[{ErrorId}] ERROR: Elitech addDevice failed. guid={Guid}, name={Name}",
                        errorId, deviceGuid, deviceName);

                    return StatusCode(502, BuildError(
                        errorId: errorId,
                        message: "Thêm Thiết Bị Thất Bại. Vui Lòng Thử Lại!",
                        ex: exUp,
                        upstreamBody: TryExtractUpstreamBody(exUp),
                        upstreamStatus: TryExtractUpstreamStatus(exUp)
                    ));
                }

                // Elitech trả về nhưng code != 0
                if (resp == null || resp.code != 0)
                {
                    _logger.LogWarning(
                        "[{ErrorId}] Elitech addDevice returned error. guid={Guid}, name={Name}, code={Code}, msg={Msg}",
                        errorId, deviceGuid, deviceName, resp?.code, resp?.msg ?? resp?.message);

                    return StatusCode(502, new
                    {
                        errorId,
                        code = resp?.code ?? -1,
                        message = resp?.msg ?? resp?.message ?? "Upstream error",
                        upstreamError = resp?.error,
                        deviceGuid,
                        deviceName,
                        time = resp?.time
                    });
                }

                // 2) Lưu registry nội bộ (Mongo)
                try
                {
                    await _db.Devices.ReplaceOneAsync(
                        x => x.DeviceGuid == deviceGuid,
                        new DeviceRegistry
                        {
                            DeviceGuid = deviceGuid,
                            DeviceName = deviceName,
                            IsActive = true,
                            AddedBy = User.Identity?.Name ?? "admin",
                            AddedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow
                        },
                        new ReplaceOptions { IsUpsert = true },
                        ct
                    );
                }
                catch (Exception exDb)
                {
                    _logger.LogError(exDb,
                        "[{ErrorId}] Đăng ký thiết bị vào MongoDB thất bại. guid={Guid}, name={Name}",
                        errorId, deviceGuid, deviceName);

                    return StatusCode(500, BuildError(
                        errorId: errorId,
                        message: "Đăng ký thiết bị vào MongoDB thất bại",
                        ex: exDb
                    ));
                }

                // 3) OK
                return Ok(new
                {
                    code = 0,
                    message = "Thêm Thiết Bị Vào Thành Công",
                    deviceGuid,
                    deviceName,
                    added = resp.data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ErrorId}] AddDevice internal error", errorId);

                return StatusCode(500, BuildError(
                    errorId: errorId,
                    message: "Lỗi Máy Chủ",
                    ex: ex
                ));
            }
        }

        // =========================
        // Helpers: error shaping
        // =========================
        private object BuildError(string errorId, string message, Exception ex, string? upstreamBody = null, int? upstreamStatus = null)
        {
            if (_env.IsDevelopment())
            {
                return new
                {
                    errorId,
                    message,
                    detail = ex.ToString(),
                    upstreamStatus,
                    upstreamBody
                };
            }

            return new
            {
                errorId,
                message
            };
        }

        private static string? TryExtractUpstreamBody(Exception ex)
        {
            var msg = ex.Message ?? "";
            var idx = msg.IndexOf("Body:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            return msg.Substring(idx + 5).Trim();
        }

        private static int? TryExtractUpstreamStatus(Exception ex)
        {
            var msg = ex.Message ?? "";
            var token = "HTTP ";
            var i = msg.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            i += token.Length;

            var j = i;
            while (j < msg.Length && char.IsDigit(msg[j])) j++;
            if (j == i) return null;

            return int.TryParse(msg.Substring(i, j - i), out var code) ? code : null;
        }
    }
}
