using Elitech.Data;
using Elitech.Models.AlertRule;
using MongoDB.Driver;

namespace Elitech.Services;

public class ElitechAlertRuleService
{
    private readonly IMongoCollection<ElitechAlertRuleViewModel> _col;

    public ElitechAlertRuleService(MongoContext ctx)
    {
        _col = ctx.Database.GetCollection<ElitechAlertRuleViewModel>("AlertRules");
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        var keys = Builders<ElitechAlertRuleViewModel>.IndexKeys
            .Ascending(x => x.UserId)
            .Ascending(x => x.DeviceGuid);

        _col.Indexes.CreateOne(new CreateIndexModel<ElitechAlertRuleViewModel>(
            keys,
            new CreateIndexOptions { Unique = true, Name = "ux_user_device_rule" }));

        _col.Indexes.CreateOne(new CreateIndexModel<ElitechAlertRuleViewModel>(
            Builders<ElitechAlertRuleViewModel>.IndexKeys.Ascending(x => x.UserId),
            new CreateIndexOptions { Name = "ix_rule_user" }));
    }

    public Task<List<ElitechAlertRuleViewModel>> GetByUserAsync(string userId, CancellationToken ct = default)
        => _col.Find(x => x.UserId == userId).SortBy(x => x.DeviceGuid).ToListAsync(ct);

    public Task<ElitechAlertRuleViewModel?> GetOneAsync(string userId, string deviceGuid, CancellationToken ct = default)
        => _col.Find(x => x.UserId == userId && x.DeviceGuid == deviceGuid).FirstOrDefaultAsync(ct);

    public Task UpsertAsync(ElitechAlertRuleViewModel rule, CancellationToken ct = default)
    {
        rule.UpdatedAtUtc = DateTime.UtcNow;

        var filter = Builders<ElitechAlertRuleViewModel>.Filter.Where(x => x.UserId == rule.UserId && x.DeviceGuid == rule.DeviceGuid);
        var update = Builders<ElitechAlertRuleViewModel>.Update
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
        => _col.DeleteOneAsync(x => x.UserId == userId && x.DeviceGuid == deviceGuid, ct);

    public Task<List<ElitechAlertRuleViewModel>> GetAllEnabledAsync(CancellationToken ct = default)
    => _col.Find(x => x.Enabled).ToListAsync(ct);

}
