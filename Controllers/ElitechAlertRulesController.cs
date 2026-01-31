using System.Security.Claims;
using Elitech.Models.AlertRule;
using Elitech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elitech.Controllers;

[ApiController]
[Route("api/elitech/alerts")]
[Authorize]
public class ElitechAlertRulesController : ControllerBase
{
    private readonly ElitechAlertRuleService _rules;
    private readonly ElitechDeviceAssignmentService _assign;

    public ElitechAlertRulesController(ElitechAlertRuleService rules, ElitechDeviceAssignmentService assign)
    {
        _rules = rules;
        _assign = assign;
    }

    [HttpGet("rules")]
    public async Task<IActionResult> GetRules(CancellationToken ct)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { code = 401, message = "No userId claim" });

        // devices của user (admin cũng dùng assignment theo userId hiện tại cho trang này)
        var devices = await _assign.GetByUserAsync(userId!, ct);

        var deviceList = devices
            .Select(d => new {
                Guid = NormalizeGuid(d.DeviceGuid),
                Name = d.DeviceName ?? ""
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Guid))
            .DistinctBy(x => x.Guid)
            .ToList();

        var guids = deviceList.Select(x => x.Guid).ToList();
        var nameMap = deviceList.ToDictionary(x => x.Guid, x => x.Name);

        // 1) rules user
        var userRules = await _rules.GetByUserAndDevicesAsync(userId!, guids, ct);
        foreach (var r in userRules) r.DeviceGuid = NormalizeGuid(r.DeviceGuid);

        // 2) rules global (admin cài)
        var globalRules = await _rules.GetByUserAndDevicesAsync("__GLOBAL__", guids, ct);
        foreach (var r in globalRules) r.DeviceGuid = NormalizeGuid(r.DeviceGuid);

        var userMap = userRules.ToDictionary(x => x.DeviceGuid, x => x);
        var globalMap = globalRules.ToDictionary(x => x.DeviceGuid, x => x);

        // 3) merge: user override global
        var merged = new List<ElitechAlertRuleViewModel>();
        foreach (var g in guids)
        {
            ElitechAlertRuleViewModel rule;
            if (userMap.TryGetValue(g, out var ur)) rule = ur;
            else if (globalMap.TryGetValue(g, out var gr)) rule = gr;
            else rule = BuildDefaultRule(userId!, g);

            // fill device name
            if (string.IsNullOrWhiteSpace(rule.DeviceName) && nameMap.TryGetValue(g, out var dn))
                rule.DeviceName = dn;

            // ensure arrays
            rule.TempRanges ??= Array.Empty<ElitechAlertRuleViewModel.Range>();
            rule.HumRanges ??= Array.Empty<ElitechAlertRuleViewModel.Range>();
            rule.TempRanges = EnsureSize(rule.TempRanges, 4);
            rule.HumRanges = EnsureSize(rule.HumRanges, 2);

            merged.Add(rule);
        }

        return Ok(new
        {
            devices = deviceList.Select(x => new { deviceGuid = x.Guid, deviceName = x.Name }),
            data = merged
        });
    }

    private static ElitechAlertRuleViewModel BuildDefaultRule(string userId, string guid) => new()
    {
        UserId = userId,
        Scope = "USER",
        DeviceGuid = guid,
        Enabled = false,
        DebounceHits = 2,
        CooldownSeconds = 180,
        UpdatedAtUtc = DateTime.UtcNow,
        TempRanges = EnsureSize(Array.Empty<ElitechAlertRuleViewModel.Range>(), 4),
        HumRanges = EnsureSize(Array.Empty<ElitechAlertRuleViewModel.Range>(), 2),
    };


    // POST /api/elitech/alerts/rules
    [HttpPost("rules")]
    public async Task<IActionResult> UpsertRule([FromBody] ElitechAlertRuleViewModel? input, CancellationToken ct)
    {
        var callerId = GetUserId();
        if (string.IsNullOrWhiteSpace(callerId))
            return Unauthorized(new { code = 401, message = "No userId claim" });

        if (input == null)
            return BadRequest(new { code = "BAD_REQUEST", message = "Body is required" });

        var guid = NormalizeGuid(input.DeviceGuid);
        if (string.IsNullOrWhiteSpace(guid))
            return BadRequest(new { code = "BAD_REQUEST", message = "deviceGuid is required" });

        // normalize arrays
        input.TempRanges ??= Array.Empty<ElitechAlertRuleViewModel.Range>();
        input.HumRanges ??= Array.Empty<ElitechAlertRuleViewModel.Range>();
        input.TempRanges = EnsureSize(input.TempRanges, 4);
        input.HumRanges = EnsureSize(input.HumRanges, 2);

        input.DeviceGuid = guid;
        input.UpdatedAtUtc = DateTime.UtcNow;

        // Quyền:
        // - User thường: chỉ được set USER cho chính mình
        // - Admin: được set GLOBAL hoặc set USER cho user khác (qua TargetUserId)
        var scope = (input.Scope ?? "USER").Trim().ToUpperInvariant();
        if (!User.IsInRole("Admin"))
        {
            scope = "USER";
            input.UserId = callerId!;
            input.Scope = "USER";

            // user chỉ set cho device đã gán
            var ok = await _assign.IsAssignedAsync(callerId!, guid, ct);
            if (!ok) return StatusCode(403, new { code = "NOT_ASSIGNED", message = "Device not assigned" });
        }
        else
        {
            if (scope == "GLOBAL")
            {
                input.Scope = "GLOBAL";
                input.UserId = "__GLOBAL__";
            }
            else
            {
                input.Scope = "USER";
                input.UserId = string.IsNullOrWhiteSpace(input.TargetUserId) ? callerId! : input.TargetUserId;
            }
        }

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
            var ok = await _assign.IsAssignedAsync(userId!, guid, ct);
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

        await _rules.DeleteAsync(userId!, guid, ct);
        return Ok(new { ok = true });
    }

    // ===== helpers =====
    private static string NormalizeGuid(string? s)
        => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToUpperInvariant();

    private static ElitechAlertRuleViewModel.Range[] EnsureSize(ElitechAlertRuleViewModel.Range[] arr, int size)
    {
        var list = (arr ?? Array.Empty<ElitechAlertRuleViewModel.Range>()).ToList();
        while (list.Count < size) list.Add(new ElitechAlertRuleViewModel.Range());
        if (list.Count > size) list = list.Take(size).ToList();
        return list.ToArray();
    }

    private string? GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        id ??= User.FindFirstValue("sub");
        // nếu hệ bạn dùng claim khác thì thêm ở đây:
        // id ??= User.FindFirstValue("uid");
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }
}