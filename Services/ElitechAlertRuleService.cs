using Elitech.Data;
using Elitech.Models.AlertRule;
using MongoDB.Driver;

namespace Elitech.Services;

public class ElitechAlertRuleService
{
    private readonly IMongoCollection<ElitechAlertRuleViewModel> _col;

    // ✅ dùng chung cho global rules
    public const string GlobalUserId = "__GLOBAL__";

    public ElitechAlertRuleService(MongoContext ctx)
    {
        _col = ctx.Database.GetCollection<ElitechAlertRuleViewModel>("AlertRules");
        EnsureIndexes();
    }

    // ✅ chuẩn hóa guid để query Mongo (case-sensitive)
    private static string NormalizeGuid(string? s)
        => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToUpperInvariant();

    private void EnsureIndexes()
    {
        // unique theo (UserId, DeviceGuid) => USER + GLOBAL đều nằm chung collection
        var keys = Builders<ElitechAlertRuleViewModel>.IndexKeys
            .Ascending(x => x.UserId)
            .Ascending(x => x.DeviceGuid);

        _col.Indexes.CreateOne(new CreateIndexModel<ElitechAlertRuleViewModel>(
            keys,
            new CreateIndexOptions { Unique = true, Name = "ux_user_device_rule" }));

        _col.Indexes.CreateOne(new CreateIndexModel<ElitechAlertRuleViewModel>(
            Builders<ElitechAlertRuleViewModel>.IndexKeys.Ascending(x => x.UserId),
            new CreateIndexOptions { Name = "ix_rule_user" }));

        _col.Indexes.CreateOne(new CreateIndexModel<ElitechAlertRuleViewModel>(
            Builders<ElitechAlertRuleViewModel>.IndexKeys.Ascending(x => x.DeviceGuid),
            new CreateIndexOptions { Name = "ix_rule_device" }));
    }

    public Task<List<ElitechAlertRuleViewModel>> GetByUserAsync(string userId, CancellationToken ct = default)
        => _col.Find(x => x.UserId == userId).SortBy(x => x.DeviceGuid).ToListAsync(ct);

    public Task<ElitechAlertRuleViewModel?> GetOneAsync(string userId, string deviceGuid, CancellationToken ct = default)
    {
        deviceGuid = NormalizeGuid(deviceGuid);
        return _col.Find(x => x.UserId == userId && x.DeviceGuid == deviceGuid).FirstOrDefaultAsync(ct);
    }

    // ✅ Controller của bạn đang gọi hàm này
    public Task<List<ElitechAlertRuleViewModel>> GetByUserAndDevicesAsync(
        string userId,
        IEnumerable<string> deviceGuids,
        CancellationToken ct = default)
    {
        var guids = (deviceGuids ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeGuid)
            .Distinct()
            .ToList();

        if (guids.Count == 0)
            return Task.FromResult(new List<ElitechAlertRuleViewModel>());

        var filter = Builders<ElitechAlertRuleViewModel>.Filter.And(
            Builders<ElitechAlertRuleViewModel>.Filter.Eq(x => x.UserId, userId),
            Builders<ElitechAlertRuleViewModel>.Filter.In(x => x.DeviceGuid, guids)
        );

        return _col.Find(filter).ToListAsync(ct);
    }

    public Task UpsertAsync(ElitechAlertRuleViewModel rule, CancellationToken ct = default)
    {
        // ✅ normalize + server time
        rule.DeviceGuid = NormalizeGuid(rule.DeviceGuid);
        rule.UpdatedAtUtc = DateTime.UtcNow;

        // guard (tránh insert bậy)
        if (string.IsNullOrWhiteSpace(rule.UserId))
            throw new ArgumentException("rule.UserId is required");
        if (string.IsNullOrWhiteSpace(rule.DeviceGuid))
            throw new ArgumentException("rule.DeviceGuid is required");

        // scope default
        rule.Scope = string.IsNullOrWhiteSpace(rule.Scope) ? "USER" : rule.Scope.Trim().ToUpperInvariant();
        if (rule.Scope != "USER" && rule.Scope != "GLOBAL")
            rule.Scope = "USER";

        // TargetUserId: tránh lưu rỗng linh tinh (optional)
        rule.TargetUserId = string.IsNullOrWhiteSpace(rule.TargetUserId) ? null : rule.TargetUserId.Trim();

        var filter = Builders<ElitechAlertRuleViewModel>.Filter.Where(
            x => x.UserId == rule.UserId && x.DeviceGuid == rule.DeviceGuid);

        var update = Builders<ElitechAlertRuleViewModel>.Update
            // ✅ chỉ update Scope 1 lần
            .Set(x => x.Scope, rule.Scope)
            .Set(x => x.TargetUserId, rule.TargetUserId)

            .Set(x => x.DeviceName, rule.DeviceName)
            .Set(x => x.TempRanges, rule.TempRanges)
            .Set(x => x.HumRanges, rule.HumRanges)
            .Set(x => x.DebounceHits, rule.DebounceHits)
            .Set(x => x.CooldownSeconds, rule.CooldownSeconds)
            .Set(x => x.Enabled, rule.Enabled)
            .Set(x => x.UpdatedAtUtc, rule.UpdatedAtUtc)

            .SetOnInsert(x => x.UserId, rule.UserId)
            .SetOnInsert(x => x.DeviceGuid, rule.DeviceGuid);

        return _col.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
    }

    public Task DeleteAsync(string userId, string deviceGuid, CancellationToken ct = default)
    {
        deviceGuid = NormalizeGuid(deviceGuid);
        return _col.DeleteOneAsync(x => x.UserId == userId && x.DeviceGuid == deviceGuid, ct);
    }

    public Task<List<ElitechAlertRuleViewModel>> GetAllEnabledAsync(CancellationToken ct = default)
        => _col.Find(x => x.Enabled).ToListAsync(ct);

    // (Optional) lấy enabled theo 1 user (hoặc GLOBAL)
    public Task<List<ElitechAlertRuleViewModel>> GetEnabledByUserAsync(string userId, CancellationToken ct = default)
        => _col.Find(x => x.UserId == userId && x.Enabled).ToListAsync(ct);
}
