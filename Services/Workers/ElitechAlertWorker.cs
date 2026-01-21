using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elitech.Models.Alerts;
using Elitech.Models.AlertRule;
using Elitech.Models.RealtimeData;
using Elitech.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elitech.Workers
{
    public class ElitechAlertWorker : BackgroundService
    {
        private readonly ILogger<ElitechAlertWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        // poll interval
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(120);

        public ElitechAlertWorker(
            ILogger<ElitechAlertWorker> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ElitechAlertWorker started (interval={IntervalSec}s)", Interval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var ruleSvc = scope.ServiceProvider.GetRequiredService<ElitechAlertRuleService>();
                    var eventSvc = scope.ServiceProvider.GetRequiredService<ElitechAlertEventService>();
                    var api = scope.ServiceProvider.GetRequiredService<ElitechApiClient>();

                    // 1) load enabled rules
                    var rules = await ruleSvc.GetAllEnabledAsync(stoppingToken);

                    if (rules == null || rules.Count == 0)
                    {
                        await Task.Delay(Interval, stoppingToken);
                        continue;
                    }

                    // 2) process per user
                    foreach (var gUser in rules.GroupBy(r => r.UserId))
                    {
                        var userId = gUser.Key;
                        var userRules = gUser.ToList();

                        // collect distinct device guids for this user
                        var guids = userRules
                            .Select(r => NormalizeGuid(r.DeviceGuid))
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();

                        if (guids.Length == 0)
                            continue;

                        // 3) realtime fetch (batched)
                        RealTimeResp? rt = null;
                        try
                        {
                            rt = await api.GetRealTimeDataAsync(guids, stoppingToken);
                        }
                        catch (Exception exApi)
                        {
                            _logger.LogWarning(exApi,
                                "Realtime fetch failed for user={UserId} devices={Count}",
                                userId, guids.Length);
                            continue;
                        }

                        var rows = rt?.data ?? new List<RealTimeItem>();

                        // map by guid
                        var map = new Dictionary<string, RealTimeItem>(StringComparer.OrdinalIgnoreCase);
                        foreach (var row in rows)
                        {
                            var guid = NormalizeGuid(row.deviceGuid);
                            if (!string.IsNullOrWhiteSpace(guid))
                                map[guid] = row;
                        }

                        // 4) evaluate each rule
                        foreach (var rule in userRules)
                        {
                            var guid = NormalizeGuid(rule.DeviceGuid);
                            if (string.IsNullOrWhiteSpace(guid))
                                continue;

                            if (!map.TryGetValue(guid, out var row))
                                continue;

                            // sample timestamp (unix seconds) - FE uses tsSecToLocal(lastSessionTime)
                            long? sampleTs = TryGetUnixSeconds(row.lastSessionTime);

                            // get state
                            var state = await eventSvc.GetStateAsync(userId, guid, stoppingToken)
                                       ?? new ElitechAlertState
                                       {
                                           UserId = userId,
                                           DeviceGuid = guid
                                       };

                            // If timestamp exists and NOT changed -> skip completely (no +1)
                            if (sampleTs.HasValue &&
                                state.LastSampleTs.HasValue &&
                                state.LastSampleTs.Value == sampleTs.Value)
                            {
                                continue;
                            }

                            // mark new sample processed
                            if (sampleTs.HasValue)
                                state.LastSampleTs = sampleTs.Value;

                            // parse sensors
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

                            // debounce by SAMPLE (only runs when sample is new)
                            if (isBadRaw) state.ConsecutiveBadHits = Math.Min(state.ConsecutiveBadHits + 1, 999);
                            else state.ConsecutiveBadHits = 0;

                            var debounceHits = Math.Max(1, rule.DebounceHits);
                            var cooldownSec = Math.Max(0, rule.CooldownSeconds);

                            var nowBad = isBadRaw && state.ConsecutiveBadHits >= debounceHits;

                            var nowUtc = DateTime.UtcNow;
                            var canCooldown = state.LastAlertAtUtc == null ||
                                              (nowUtc - state.LastAlertAtUtc.Value).TotalSeconds >= cooldownSec;

                            var reasonsNorm = (reasons ?? "").Trim();
                            var reasonsChanged = !string.Equals(
                                (state.LastReasons ?? "").Trim(),
                                reasonsNorm,
                                StringComparison.OrdinalIgnoreCase);

                            // Fire when: nowBad + pass cooldown + (was not bad OR reasons changed)
                            var fire = nowBad && canCooldown && (!state.IsBad || reasonsChanged);

                            if (fire)
                            {
                                var deviceName = row.deviceName ?? rule.DeviceName ?? guid;

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

                                _logger.LogInformation(
                                    "ALERT fired user={UserId} guid={Guid} name={Name} reasons={Reasons}",
                                    userId, guid, deviceName, reasonsNorm);
                            }

                            // update state flags
                            state.IsBad = nowBad;
                            if (!nowBad) state.LastReasons = "";

                            await eventSvc.UpsertStateAsync(state, stoppingToken);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // graceful shutdown
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

        private static long? TryGetUnixSeconds(object? value)
        {
            if (value == null) return null;

            try
            {
                // Elitech API sometimes returns string/number => Convert handles most
                var x = Convert.ToInt64(value);
                return x > 0 ? x : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
