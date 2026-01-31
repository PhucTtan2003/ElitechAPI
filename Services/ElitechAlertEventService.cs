using Elitech.Data;
using Elitech.Models.Alerts;
using MongoDB.Driver;

namespace Elitech.Services;

public class ElitechAlertEventService
{
    private readonly IMongoCollection<ElitechAlertEvent> _events;
    private readonly IMongoCollection<ElitechAlertState> _states;

    public ElitechAlertEventService(MongoContext ctx)
    {
        _events = ctx.Database.GetCollection<ElitechAlertEvent>("AlertEvents");
        _states = ctx.Database.GetCollection<ElitechAlertState>("AlertStates");
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        _events.Indexes.CreateOne(new CreateIndexModel<ElitechAlertEvent>(
            Builders<ElitechAlertEvent>.IndexKeys.Ascending(x => x.UserId).Descending(x => x.OccurredAtUtc),
            new CreateIndexOptions { Name = "ix_event_user_time" }));

        _states.Indexes.CreateOne(new CreateIndexModel<ElitechAlertState>(
            Builders<ElitechAlertState>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.DeviceGuid),
            new CreateIndexOptions { Unique = true, Name = "ux_state_user_device" }));
    }

    public Task<ElitechAlertState?> GetStateAsync(string userId, string deviceGuid, CancellationToken ct = default)
        => _states.Find(x => x.UserId == userId && x.DeviceGuid == deviceGuid).FirstOrDefaultAsync(ct);

    public Task UpsertStateAsync(ElitechAlertState s, CancellationToken ct = default)
    {
        s.UpdatedAtUtc = DateTime.UtcNow;
        var filter = Builders<ElitechAlertState>.Filter.Where(x => x.UserId == s.UserId && x.DeviceGuid == s.DeviceGuid);
        var update = Builders<ElitechAlertState>.Update
            .Set(x => x.ConsecutiveBadHits, s.ConsecutiveBadHits)
            .Set(x => x.IsBad, s.IsBad)
            .Set(x => x.LastAlertAtUtc, s.LastAlertAtUtc)
            .Set(x => x.LastReasons, s.LastReasons)
            .Set(x => x.UpdatedAtUtc, s.UpdatedAtUtc)
            .Set(x => x.LastSampleTs, s.LastSampleTs)
            .SetOnInsert(x => x.UserId, s.UserId)
            .SetOnInsert(x => x.DeviceGuid, s.DeviceGuid);

        return _states.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
    }

    public Task InsertEventAsync(ElitechAlertEvent e, CancellationToken ct = default)
        => _events.InsertOneAsync(e, cancellationToken: ct);
}