using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Elitech.Models.AlertRule;
using Elitech.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elitech.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/elitech/alerts")]
    public class ElitechAlertRulesController : ControllerBase
    {
        private readonly ElitechAlertRuleService _rules;
        private readonly ElitechDeviceAssignmentService _assign;

        public ElitechAlertRulesController(
            ElitechAlertRuleService rules,
            ElitechDeviceAssignmentService assign)
        {
            _rules = rules;
            _assign = assign;
        }

        // GET /api/elitech/alerts/rules
        [HttpGet("rules")]
        public async Task<IActionResult> GetRules(CancellationToken ct)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { code = 401, message = "No userId claim" });

            var list = await _rules.GetByUserAsync(userId, ct);
            return Ok(new { data = list });
        }

        // POST /api/elitech/alerts/rules
        [HttpPost("rules")]
        public async Task<IActionResult> UpsertRule([FromBody] ElitechAlertRuleViewModel input, CancellationToken ct)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { code = 401, message = "No userId claim" });

            if (input == null)
                return BadRequest(new { code = "BAD_REQUEST", message = "Body is required" });

            // Normalize GUID (Mongo string match case-sensitive)
            var guid = NormalizeGuid(input.DeviceGuid);
            if (string.IsNullOrWhiteSpace(guid))
                return BadRequest(new { code = "BAD_REQUEST", message = "deviceGuid is required" });

            // User chỉ set rule cho device đã gán (Admin bypass)
            if (!User.IsInRole("Admin"))
            {
                var ok = await _assign.IsAssignedAsync(userId, guid, ct);
                if (!ok)
                {
                    return StatusCode(403, new
                    {
                        code = "NOT_ASSIGNED",
                        message = "Device is not assigned to this user",
                        deviceGuid = guid,
                        userId
                    });
                }
            }

            // Normalize array sizes (UI gửi thiếu vẫn không crash)
            input.TempRanges ??= Array.Empty<ElitechAlertRuleViewModel.Range>();
            input.HumRanges ??= Array.Empty<ElitechAlertRuleViewModel.Range>();

            input.TempRanges = EnsureSize(input.TempRanges, 4);
            input.HumRanges = EnsureSize(input.HumRanges, 2);

            // Server-owned fields
            input.UserId = userId;
            input.DeviceGuid = guid;

            await _rules.UpsertAsync(input, ct);

            return Ok(new { ok = true });
        }

        // DELETE /api/elitech/alerts/rules?deviceGuid=...
        [HttpDelete("rules")]
        public async Task<IActionResult> Delete([FromQuery] string deviceGuid, CancellationToken ct)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { code = 401, message = "No userId claim" });

            var guid = NormalizeGuid(deviceGuid);
            if (string.IsNullOrWhiteSpace(guid))
                return BadRequest(new { code = "BAD_REQUEST", message = "deviceGuid required" });

            if (!User.IsInRole("Admin"))
            {
                var ok = await _assign.IsAssignedAsync(userId, guid, ct);
                if (!ok)
                {
                    return StatusCode(403, new
                    {
                        code = "NOT_ASSIGNED",
                        message = "Device is not assigned to this user",
                        deviceGuid = guid,
                        userId
                    });
                }
            }

            await _rules.DeleteAsync(userId, guid, ct);
            return Ok(new { ok = true });
        }

        // =========================
        // Helpers
        // =========================
        private static string NormalizeGuid(string? s)
            => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToUpperInvariant();

        private static ElitechAlertRuleViewModel.Range[] EnsureSize(ElitechAlertRuleViewModel.Range[] arr, int size)
        {
            var list = (arr ?? Array.Empty<ElitechAlertRuleViewModel.Range>()).ToList();

            while (list.Count < size)
                list.Add(new ElitechAlertRuleViewModel.Range());

            if (list.Count > size)
                list = list.Take(size).ToList();

            return list.ToArray();
        }

        private string? GetUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");
            // Nếu hệ bạn dùng claim khác thì mở thêm:
            // id ??= User.FindFirstValue("uid");

            return string.IsNullOrWhiteSpace(id) ? null : id;
        }
    }
}
