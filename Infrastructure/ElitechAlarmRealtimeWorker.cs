using Elitech.Hubs;
using Elitech.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;

namespace Elitech.Infrastructure;

public class ElitechAlarmRealtimeWorker : BackgroundService
{
    private readonly ILogger<ElitechAlarmRealtimeWorker> _logger;
    private readonly ElitechApiClient _api;
    private readonly IHubContext<ElitechAlarmHub> _hub;

    // cache lastSeen per deviceGuid
    private readonly IMemoryCache _cache;

    // interval poll server-side
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan WindowBack = TimeSpan.FromMinutes(5);

    public ElitechAlarmRealtimeWorker(
        ILogger<ElitechAlarmRealtimeWorker> logger,
        ElitechApiClient api,
        IHubContext<ElitechAlarmHub> hub,
        IMemoryCache cache)
    {
        _logger = logger;
        _api = api;
        _hub = hub;
        _cache = cache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ElitechAlarmRealtimeWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Lưu ý: nếu bạn muốn tối ưu hơn, bạn có thể track danh sách device đang được subscribe
                // bằng 1 registry. Ở đây mình làm kiểu đơn giản: pull theo device list mà bạn quản lý.
                // => Bạn tự thay danh sách deviceGuids theo hệ thống của bạn (Mongo assignment).
                var deviceGuids = await GetActiveDeviceGuidsAsync(stoppingToken);

                foreach (var deviceGuid in deviceGuids)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    await PullAndBroadcastForDevice(deviceGuid, stoppingToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AlarmRealtime loop error");
            }

            try { await Task.Delay(PollInterval, stoppingToken); } catch { }
        }

        _logger.LogInformation("ElitechAlarmRealtimeWorker stopped.");
    }

    private async Task<List<string>> GetActiveDeviceGuidsAsync(CancellationToken ct)
    {
        // ✅ Cách 1 (demo): dùng API getDeviceGuids
        // (Bạn có thể thay bằng device được assign trong Mongo theo user để giảm tải)
        var resp = await _api.GetDeviceGuidsAsync(ct);
        return resp?.Data?.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
               ?? new List<string>();
    }

    private async Task PullAndBroadcastForDevice(string deviceGuid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deviceGuid)) return;

        var keyLast = $"alarm:last:{deviceGuid}";
        var lastSeen = _cache.TryGetValue<long>(keyLast, out var v) ? v : 0L;

        var to = DateTimeOffset.UtcNow;
        DateTimeOffset from;

        if (lastSeen > 0)
            from = DateTimeOffset.FromUnixTimeSeconds(lastSeen).Subtract(WindowBack);
        else
            from = to.AddHours(-24);

        // subUid thường = 0
        var resp = await _api.GetAlarmRecordAsync(deviceGuid, subUid: 0, from, to, ct);

        if (resp == null || resp.code != 0 || resp.data == null || resp.data.Count == 0)
            return;

        // chỉ lấy record mới hơn lastSeen (nếu lastSeen đã có)
        var items = resp.data
            .Where(x => x != null)
            .OrderBy(x => x.alarmTimeStamp ?? 0)
            .ToList();

        var newOnes = lastSeen > 0
            ? items.Where(x => (x.alarmTimeStamp ?? 0) > lastSeen).ToList()
            : items;

        if (newOnes.Count == 0) return;

        var maxTs = newOnes.Max(x => x.alarmTimeStamp ?? 0);
        _cache.Set(keyLast, maxTs, TimeSpan.FromHours(6));

        // push xuống group theo deviceGuid
        var group = $"dev:{deviceGuid.Trim()}";
        await _hub.Clients.Group(group).SendAsync("alarm.new", newOnes, ct);
    }
}
