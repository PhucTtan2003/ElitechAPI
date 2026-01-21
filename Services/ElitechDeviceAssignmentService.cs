using Elitech.Data;
using Elitech.Models;
using MongoDB.Driver;

namespace Elitech.Services;

public class ElitechDeviceAssignmentService
{
    private readonly IMongoCollection<ElitechDeviceAssignment> _col;

    public ElitechDeviceAssignmentService(MongoContext ctx)
    {
        _col = ctx.Database.GetCollection<ElitechDeviceAssignment>("DeviceAssignments");
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        var keys = Builders<ElitechDeviceAssignment>.IndexKeys
            .Ascending(x => x.UserId)
            .Ascending(x => x.DeviceGuid);

        _col.Indexes.CreateOne(new CreateIndexModel<ElitechDeviceAssignment>(
            keys,
            new CreateIndexOptions { Unique = true, Name = "ux_user_device" }));

        _col.Indexes.CreateOne(new CreateIndexModel<ElitechDeviceAssignment>(
            Builders<ElitechDeviceAssignment>.IndexKeys.Ascending(x => x.UserId),
            new CreateIndexOptions { Name = "ix_user" }));
    }

    public Task<List<ElitechDeviceAssignment>> GetByUserAsync(string userId, CancellationToken ct = default)
        => _col.Find(x => x.UserId == userId).SortBy(x => x.DeviceGuid).ToListAsync(ct);

    public async Task<HashSet<string>> GetDeviceGuidsOfUserAsync(string userId, CancellationToken ct = default)
    {
        var list = await _col.Find(x => x.UserId == userId).Project(x => x.DeviceGuid).ToListAsync(ct);
        return new HashSet<string>(list ?? new(), StringComparer.OrdinalIgnoreCase);
    }

    public Task<bool> IsAssignedAsync(string userId, string deviceGuid, CancellationToken ct = default)
    {
        deviceGuid = (deviceGuid ?? "").Trim();
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(deviceGuid)) return Task.FromResult(false);
        return _col.Find(x => x.UserId == userId && x.DeviceGuid == deviceGuid).AnyAsync(ct);
    }

    public Task AssignAsync(string userId, string deviceGuid, string? deviceName, CancellationToken ct = default)
    {
        deviceGuid = (deviceGuid ?? "").Trim();
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId required");
        if (string.IsNullOrWhiteSpace(deviceGuid)) throw new ArgumentException("deviceGuid required");

        var filter = Builders<ElitechDeviceAssignment>.Filter.Where(x => x.UserId == userId && x.DeviceGuid == deviceGuid);
        var update = Builders<ElitechDeviceAssignment>.Update
            .SetOnInsert(x => x.UserId, userId)
            .SetOnInsert(x => x.DeviceGuid, deviceGuid)
            .Set(x => x.DeviceName, deviceName)
            .Set(x => x.AssignedAtUtc, DateTime.UtcNow);

        return _col.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
    }

    public async Task<bool> UnassignAsync(string userId, string deviceGuid, CancellationToken ct = default)
    {
        deviceGuid = (deviceGuid ?? "").Trim();
        var res = await _col.DeleteOneAsync(x => x.UserId == userId && x.DeviceGuid == deviceGuid, ct);
        return res.DeletedCount > 0;
    }
    public Task<List<ElitechDeviceAssignment>> GetAllDevicesAsync(CancellationToken ct = default)
    {
        // Lấy tất cả assignment, group theo DeviceGuid để ra danh sách thiết bị unique
        return _col.Aggregate()
            .SortByDescending(x => x.AssignedAtUtc)
            .Group(x => x.DeviceGuid, g => g.First())
            .ToListAsync(ct);
    }


}
