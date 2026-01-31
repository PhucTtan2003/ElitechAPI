using Elitech.Models.Alerts;
using Elitech.Models.AlertRule;
using Elitech.Models.RealtimeData;
using Elitech.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elitech.Workers;

public class ElitechAlertWorker : BackgroundService
{
    private readonly ILogger<ElitechAlertWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    // Worker tick: có thể 60–120s, debounce vẫn theo SAMPLE nên không spam
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(120);

    public ElitechAlertWorker(ILogger<ElitechAlertWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ElitechAlertWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var ruleSvc = scope.ServiceProvider.GetRequiredService<ElitechAlertRuleService>();
                var eventSvc = scope.ServiceProvider.GetRequiredService<ElitechAlertEventService>();
                var api = scope.ServiceProvider.GetRequiredService<ElitechApiClient>();

                // ✅ cache realtime để Realtime API đọc, tránh gọi upstream từ FE
                var rtCache = scope.ServiceProvider.GetService<ElitechRealtimeCacheService>(); // optional

                // 1) load enabled rules
                var rules = await ruleSvc.GetAllEnabledAsync(stoppingToken);
                if (rules.Count == 0)
                {
                    await Task.Delay(Interval, stoppingToken);
                    continue;
                }

                // 2) group theo user để batch realtime theo user
                foreach (var gUser in rules.GroupBy(r => r.UserId))
                {
                    var userId = gUser.Key;
                    var userRules = gUser.ToList();

                    var guids = userRules
                        .Select(r => NormalizeGuid(r.DeviceGuid))
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    if (guids.Length == 0) continue;

                    // 3) pull realtime 1 lần cho cả user
                    RealTimeResp? rt = null;
                    try
                    {
                        rt = await api.GetRealTimeDataAsync(guids, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        // ✅ nếu upstream fail, log + skip user này, tránh crash cả tick
                        _logger.LogWarning(ex, "Realtime pull failed for user {UserId}", userId);
                        continue;
                    }

                    var rows = rt?.data ?? new List<RealTimeItem>();

                    // ✅ đổ vào cache (nếu có)
                    if (rtCache != null)
                    {
                        foreach (var row in rows)
                        {
                            var g = NormalizeGuid(row.deviceGuid);
                            if (!string.IsNullOrWhiteSpace(g))
                                rtCache.Upsert(g, row);
                        }
                    }

                    // map typed by guid
                    var map = new Dictionary<string, RealTimeItem>(StringComparer.OrdinalIgnoreCase);
                    foreach (var row in rows)
                    {
                        var guid = NormalizeGuid(row.deviceGuid);
                        if (!string.IsNullOrWhiteSpace(guid))
                            map[guid] = row;
                    }

                    // 4) evaluate từng rule
                    foreach (var rule in userRules)
                    {
                        var guid = NormalizeGuid(rule.DeviceGuid);
                        if (string.IsNullOrWhiteSpace(guid)) continue;
                        if (!map.TryGetValue(guid, out var row)) continue;

                        // ✅ sample timestamp: ưu tiên lastSessionTime (unix seconds), fallback: null
                        var sampleTs = TryGetSampleTs(row);

                        // load state
                        var state = await eventSvc.GetStateAsync(userId, guid, stoppingToken)
                                   ?? new ElitechAlertState { UserId = userId, DeviceGuid = guid };

                        // ✅ Nếu sampleTs có và không đổi => không tính debounce nữa (không +1)
                        // BUT vẫn nên update "LastSeenAtUtc" (nếu bạn có field) hoặc ít nhất giữ state ổn định
                        if (sampleTs.HasValue && state.LastSampleTs.HasValue && state.LastSampleTs.Value == sampleTs.Value)
                        {
                            // Nếu bạn có field UpdatedAtUtc / LastSeenAtUtc thì update ở đây:
                            // state.UpdatedAtUtc = DateTime.UtcNow;
                            // await eventSvc.UpsertStateAsync(state, stoppingToken);

                            continue;
                        }

                        // ✅ sample mới => lưu dấu
                        if (sampleTs.HasValue)
                            state.LastSampleTs = sampleTs.Value;

                        // parse tmp/hum 4+2 (typed)
                        var temps = new double?[4]
                        {
                            ElitechAlertEngine.ParseNumber(row.tmp1),
                            ElitechAlertEngine.ParseNumber(row.tmp2),
                            ElitechAlertEngine.ParseNumber(row.tmp3),
                            ElitechAlertEngine.ParseNumber(row.tmp4),
                        };
                        var hums = new double?[2]
                        {
                            ElitechAlertEngine.ParseNumber(row.hum1),
                            ElitechAlertEngine.ParseNumber(row.hum2),
                        };

                        var (isBadRaw, reasons) = ElitechAlertEngine.Evaluate(rule, temps, hums);

                        // ✅ debounce theo SAMPLE (vì block trên đã đảm bảo chỉ chạy khi sample mới)
                        if (isBadRaw) state.ConsecutiveBadHits = Math.Min(state.ConsecutiveBadHits + 1, 999);
                        else state.ConsecutiveBadHits = 0;

                        var debounce = Math.Max(1, rule.DebounceHits);
                        var cooldown = Math.Max(0, rule.CooldownSeconds);

                        var nowBad = isBadRaw && state.ConsecutiveBadHits >= debounce;

                        var nowUtc = DateTime.UtcNow;
                        var canCooldown = state.LastAlertAtUtc == null
                            || (nowUtc - state.LastAlertAtUtc.Value).TotalSeconds >= cooldown;

                        var reasonsNorm = (reasons ?? "").Trim();
                        var reasonsChanged = !string.Equals(state.LastReasons ?? "", reasonsNorm, StringComparison.OrdinalIgnoreCase);

                        // ✅ TEST: nhắc lại mỗi 2 hit khi vẫn BAD
                        const int RepeatEveryHits_Test = 2;

                        // nhắc lại khi vẫn BAD và hit là bội số của 2
                        var hitReminder = nowBad
                            && state.ConsecutiveBadHits >= debounce
                            && (state.ConsecutiveBadHits % RepeatEveryHits_Test == 0);

                        // ✅ fire khi:
                        // - OK -> BAD
                        // - hoặc vẫn BAD nhưng reasons đổi
                        // - hoặc vẫn BAD và tới mốc nhắc lại (mỗi 2 hit)
                        var fire = nowBad && canCooldown && (!state.IsBad || reasonsChanged || hitReminder);


                        if (fire)
                        {
                            var deviceName = row.deviceName ?? rule.DeviceName;

                            await eventSvc.InsertEventAsync(new ElitechAlertEvent
                            {
                                UserId = userId,
                                DeviceGuid = guid,
                                DeviceName = deviceName,
                                OccurredAtUtc = nowUtc,

                                Tmp1 = temps[0],
                                Tmp2 = temps[1],
                                Tmp3 = temps[2],
                                Tmp4 = temps[3],
                                Hum1 = hums[0],
                                Hum2 = hums[1],

                                Reasons = reasonsNorm,
                                Level = "ALARM",
                                IsRead = false
                            }, stoppingToken);

                            state.LastAlertAtUtc = nowUtc;
                            state.LastReasons = reasonsNorm;
                        }

                        // ✅ update state cuối cùng
                        state.IsBad = nowBad;

                        // Nếu về OK thì reset reasons (tuỳ bạn). Mình giữ cách bạn đang làm.
                        if (!nowBad)
                            state.LastReasons = "";

                        await eventSvc.UpsertStateAsync(state, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ElitechAlertWorker tick error");
            }

            await Task.Delay(Interval, stoppingToken);
        }

        _logger.LogInformation("ElitechAlertWorker stopped");
    }

    private static string NormalizeGuid(string? s)
        => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToUpperInvariant();

    /// <summary>
    /// Try parse lastSessionTime -> unix seconds (long).
    /// Nếu API trả string/long/double… đều cố gắng parse.
    /// </summary>
    private static long? TryGetSampleTs(RealTimeItem row)
    {
        try
        {
            var v = row.lastSessionTime;
            if (v == null) return null;

            // nếu đã là long/int
            if (v is long l) return l;
            if (v is long i) return i;

            // nếu là double (unix seconds)
            if (v is long d) return (long)d;

            // nếu là string
            var s = v.ToString();
            if (string.IsNullOrWhiteSpace(s)) return null;

            // đôi khi server trả "1712345678.0"
            if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dd))
                return (long)dd;

            if (long.TryParse(s, out var ll))
                return ll;

            return null;
        }
        catch
        {
            return null;
        }
    }
}