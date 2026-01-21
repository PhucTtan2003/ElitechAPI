using Elitech.Models;
using Elitech.Models.DeviceInfo;
using Elitech.Models.HistoryData;
using Elitech.Models.RealtimeData;
using Elitech.Models.SetParam;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Elitech.Models.Waybill.BatchWaybillViewModel;

namespace Elitech.Services;

public class ElitechApiClient
{
    private readonly HttpClient _http;
    private readonly ElitechOptions _opt;
    private readonly HttpClient _httpAlarm;
    private readonly ElitechAlarmOptions _alarmOpt;
    // token cache
    private string? _cachedToken;
    private DateTimeOffset _tokenGotAt;

    // token anti-spam (5110)
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private DateTimeOffset _lastTokenAttemptAt = DateTimeOffset.MinValue;

    // device guids cache
    private List<string>? _cachedDeviceGuids;
    private DateTimeOffset _deviceGuidsCachedAt;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ElitechApiClient(
         IHttpClientFactory factory,
         ElitechOptions opt,
         ElitechAlarmOptions alarmOpt)
    {
        _opt = opt;
        _alarmOpt = alarmOpt;

        _http = factory.CreateClient("ElitechMain");
        _httpAlarm = factory.CreateClient("ElitechAlarm");

        _http.BaseAddress = new Uri(_opt.BaseUrl.TrimEnd('/'));
        _httpAlarm.BaseAddress = new Uri(_alarmOpt.BaseUrl.TrimEnd('/'));
    }

    // =========================
    // Token (fix 5110: once per minute)
    // =========================
    private async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        // dùng token cache nếu còn hạn
        if (!string.IsNullOrEmpty(_cachedToken) &&
            DateTimeOffset.UtcNow - _tokenGotAt < TimeSpan.FromMinutes(30))
            return _cachedToken!;

        await _tokenLock.WaitAsync(ct);
        try
        {
            // double check sau lock
            if (!string.IsNullOrEmpty(_cachedToken) &&
                DateTimeOffset.UtcNow - _tokenGotAt < TimeSpan.FromMinutes(30))
                return _cachedToken!;

            // cooldown 60s để không spam getToken
            var now = DateTimeOffset.UtcNow;
            var since = now - _lastTokenAttemptAt;
            if (since < TimeSpan.FromSeconds(60))
            {
                var wait = TimeSpan.FromSeconds(60) - since;
                await Task.Delay(wait, ct);
            }

            _lastTokenAttemptAt = DateTimeOffset.UtcNow;

            var payload = await PostAsync<TokenResp>(
                "/api/data-api/elitechAccess/getToken",
                new
                {
                    keyId = _opt.KeyId,
                    keySecret = _opt.KeySecret,
                    userName = _opt.UserName,
                    password = _opt.Password
                },
                auth: false,
                ct: ct);

            if (payload == null)
                throw new InvalidOperationException("Empty token response");

            var token = payload.data?.Trim();

            if (string.IsNullOrWhiteSpace(token))
            {
                // nếu đúng 5110, chờ và retry 1 lần
                if (payload.code == 5110)
                {
                    await Task.Delay(TimeSpan.FromSeconds(61), ct);

                    _lastTokenAttemptAt = DateTimeOffset.UtcNow;

                    var payload2 = await PostAsync<TokenResp>(
                        "/api/data-api/elitechAccess/getToken",
                        new
                        {
                            keyId = _opt.KeyId,
                            keySecret = _opt.KeySecret,
                            userName = _opt.UserName,
                            password = _opt.Password
                        },
                        auth: false,
                        ct: ct);

                    token = payload2?.data?.Trim();
                    if (string.IsNullOrWhiteSpace(token))
                        throw new InvalidOperationException(
                            $"Cannot get token after retry. code={payload2?.code}, msg={payload2?.msg ?? payload2?.message ?? payload2?.error}");
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Cannot get token. code={payload.code}, msg={payload.msg ?? payload.message ?? payload.error}");
                }
            }

            // chuẩn hoá Bearer
            if (token!.StartsWith("bearer ", StringComparison.OrdinalIgnoreCase))
                token = "Bearer " + token.Substring(7);
            else if (!token.StartsWith("Bearer ", StringComparison.Ordinal))
                token = "Bearer " + token;

            _cachedToken = token;
            _tokenGotAt = DateTimeOffset.UtcNow;
            return _cachedToken!;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    // =========================
    // Core Post
    // =========================
    private async Task<T?> PostAsync<T>(string endpoint, object body, bool auth, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);

        if (auth)
        {
            var token = await GetTokenAsync(ct);
            req.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
        }

        req.Content = new StringContent(
            JsonSerializer.Serialize(body, _jsonOpts),
            Encoding.UTF8,
            "application/json");

        using var res = await _http.SendAsync(req, ct);
        var content = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)res.StatusCode} when calling {endpoint}. Body: {content}");

        try
        {
            return JsonSerializer.Deserialize<T>(content, _jsonOpts);
        }
        catch (Exception ex)
        {
            throw new JsonException($"Deserialize failed for {endpoint}. Body: {content}", ex);
        }
    }

    // =========================
    // API #8 - Add Device
    // =========================
    public async Task<AddDeviceResp> AddDeviceAsync(IEnumerable<AddDeviceInfo> deviceInfos, CancellationToken ct = default)
    {
        var list = (deviceInfos ?? Enumerable.Empty<AddDeviceInfo>())
            .Where(x => x is not null
                        && !string.IsNullOrWhiteSpace(x.deviceGuid)
                        && !string.IsNullOrWhiteSpace(x.deviceName))
            .Select(x => new AddDeviceInfo(x.deviceName.Trim(), x.deviceGuid.Trim()))
            .ToList();

        if (list.Count == 0)
            throw new ArgumentException("deviceInfos is required and must not be empty.", nameof(deviceInfos));

        var resp = await PostAsync<AddDeviceResp>(
            endpoint: "/api/data-api/elitechAccess/addDevice",
            body: new
            {
                keyId = _opt.KeyId,
                keySecret = _opt.KeySecret,
                deviceInfos = list
            },
            auth: true,
            ct: ct);

        return resp ?? new AddDeviceResp { code = -1, message = "Empty response" };
    }

    // =========================
    // API #7 - Get Device GUIDs
    // =========================
    public async Task<DeviceGuidsResp> GetDeviceGuidsAsync(CancellationToken ct = default)
    {
        if (_cachedDeviceGuids != null &&
            DateTimeOffset.UtcNow - _deviceGuidsCachedAt < TimeSpan.FromSeconds(60))
        {
            return new DeviceGuidsResp { Code = 0, Data = _cachedDeviceGuids };
        }

        var resp = await PostAsync<DeviceGuidsResp>(
            "/api/data-api/elitechAccess/getDeviceGuids",
            new { keyId = _opt.KeyId, keySecret = _opt.KeySecret },
            auth: true,
            ct: ct);

        if (resp?.Code == 0 && resp.Data != null)
        {
            _cachedDeviceGuids = resp.Data;
            _deviceGuidsCachedAt = DateTimeOffset.UtcNow;
        }

        return resp ?? new DeviceGuidsResp { Code = -1, Message = "Empty response" };
    }

    // =========================
    // History
    // =========================
    public async Task<HistoryResp> GetHistoryDataAsync(
        string deviceGuid,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct = default)
    {
        var resp = await PostAsync<HistoryResp>(
            "/api/data-api/elitechAccess/getHistoryData",
            new
            {
                keyId = _opt.KeyId,
                keySecret = _opt.KeySecret,
                deviceGuid,
                startTime = start.ToUnixTimeSeconds(),
                endTime = end.ToUnixTimeSeconds()
            },
            auth: true,
            ct: ct);

        return resp ?? new HistoryResp { code = -1, message = "Empty response" };
    }

    public async Task<HistoryResp> GetHistoryDataRangeAsync(
        string deviceGuid,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct = default,
        int delayBetweenRequestsMs = 0)
    {
        if (string.IsNullOrWhiteSpace(deviceGuid))
            throw new ArgumentException("deviceGuid is required", nameof(deviceGuid));
        if (end < start)
            throw new ArgumentException("end must be >= start");

        var maxSpan = TimeSpan.FromDays(10) - TimeSpan.FromSeconds(1);

        var chunks = new List<(DateTimeOffset s, DateTimeOffset e)>();
        for (var cur = start; cur <= end;)
        {
            var next = cur + maxSpan;
            if (next > end) next = end;
            chunks.Add((cur, next));
            if (next >= end) break;
            cur = next.AddSeconds(1);
        }

        var all = new List<HistoryItem>();

        foreach (var (s, e) in chunks)
        {
            ct.ThrowIfCancellationRequested();

            var resp = await PostAsync<HistoryResp>(
                "/api/data-api/elitechAccess/getHistoryData",
                new
                {
                    keyId = _opt.KeyId,
                    keySecret = _opt.KeySecret,
                    deviceGuid,
                    startTime = s.ToUnixTimeSeconds(),
                    endTime = e.ToUnixTimeSeconds()
                },
                auth: true,
                ct: ct);

            if (resp == null)
                return new HistoryResp { code = -1, message = "Empty response" };

            if (resp.code == 0)
            {
                if (resp.data is { Count: > 0 })
                    all.AddRange(resp.data);
            }
            else
            {
                if ((e - s) > TimeSpan.FromDays(1))
                {
                    var mid = s + TimeSpan.FromTicks((e - s).Ticks / 2);

                    var left = await GetHistoryDataRangeAsync(deviceGuid, s, mid, ct, delayBetweenRequestsMs);
                    if (left.code != 0) return left;

                    var right = await GetHistoryDataRangeAsync(deviceGuid, mid.AddSeconds(1), e, ct, delayBetweenRequestsMs);
                    if (right.code != 0) return right;

                    if (left.data != null) all.AddRange(left.data);
                    if (right.data != null) all.AddRange(right.data);
                }
                else
                {
                    return resp;
                }
            }

            if (delayBetweenRequestsMs > 0)
                await Task.Delay(delayBetweenRequestsMs, ct);
        }

        var merged = all
            .GroupBy(x => (x.monitorTime ?? 0L, x.subUid ?? -1))
            .Select(g => g.First())
            .OrderBy(x => x.monitorTime ?? 0L)
            .ToList();

        return new HistoryResp
        {
            code = 0,
            message = $"Merged {merged.Count} samples from {chunks.Count} chunks",
            data = merged
        };
    }

    // =========================
    // Realtime
    // =========================
    public async Task<RealTimeResp> GetRealTimeDataAsync(IEnumerable<string> deviceGuids, CancellationToken ct = default)
    {
        var arr = deviceGuids?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToArray() ?? Array.Empty<string>();

        if (arr.Length == 0)
            throw new ArgumentException("deviceGuids is required and must not be empty.", nameof(deviceGuids));

        var resp = await PostAsync<RealTimeResp>(
            endpoint: "/api/data-api/elitechAccess/getRealTimeData",
            body: new { keyId = _opt.KeyId, keySecret = _opt.KeySecret, deviceGuids = arr },
            auth: true,
            ct: ct);

        return resp ?? new RealTimeResp { code = -1, message = "Empty response" };
    }

    public Task<RealTimeResp> GetRealTimeDataAsync(string deviceGuid, CancellationToken ct = default)
        => GetRealTimeDataAsync(new[] { deviceGuid }, ct);

    // =========================
    // Device Info
    // =========================
    public async Task<DeviceInfoResp> GetDeviceInfoAsync(IEnumerable<string> deviceGuids, CancellationToken ct = default)
    {
        var arr = deviceGuids?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        if (arr.Length == 0)
            throw new ArgumentException("deviceGuids is required.", nameof(deviceGuids));

        var resp = await PostAsync<DeviceInfoResp>(
            endpoint: "/api/data-api/elitechAccess/getDeviceInfo",
            body: new { keyId = _opt.KeyId, keySecret = _opt.KeySecret, deviceGuids = arr },
            auth: true,
            ct: ct);

        return resp ?? new DeviceInfoResp { code = -1, message = "Empty response" };
    }

    // =========================================================
    // OPTIONAL: SetParam + BatchWaybill
    // Chỉ bật nếu bạn có model + namespace tương ứng
    // =========================================================

    public async Task<SetParamResp> SetParamAsync(SetParamReq req, CancellationToken ct = default)
    {
        if (req == null) throw new ArgumentNullException(nameof(req));
        if (string.IsNullOrWhiteSpace(req.deviceGuid))
            throw new ArgumentException("deviceGuid is required.", nameof(req));

        var body = new Dictionary<string, object?>
        {
            ["keyId"] = _opt.KeyId,
            ["keySecret"] = _opt.KeySecret,
            ["deviceGuid"] = req.deviceGuid,

            ["recordInterval"] = req.recordInterval,
            ["uploadInterval"] = req.uploadInterval,

            ["tmpUpper"] = req.tmpUpper,
            ["tmpLower"] = req.tmpLower,
            ["humUpper"] = req.humUpper,
            ["humLower"] = req.humLower,

            ["waybillStartTime"] = req.waybillStartTime,
            ["waybillNum"] = req.waybillNum,
        };

        var jsonOpts = new JsonSerializerOptions(_jsonOpts)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, "/api/data-api/elitechAccess/setParam");
        var token = await GetTokenAsync(ct);
        httpReq.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
        httpReq.Content = new StringContent(JsonSerializer.Serialize(body, jsonOpts), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(httpReq, ct);
        var content = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)res.StatusCode} setParam. Body: {content}");

        return JsonSerializer.Deserialize<SetParamResp>(content, _jsonOpts)
               ?? new SetParamResp { code = -1, message = "Empty response" };
    }

    public async Task<BatchWaybillResp> BatchSetWaybillAsync(
        IEnumerable<string> deviceGuids,
        long? start,
        long? stop,
        CancellationToken ct = default)
    {
        var arr = (deviceGuids ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (arr.Length == 0)
            throw new ArgumentException("deviceGuids is required.");
        if (arr.Length > 200)
            throw new ArgumentException("deviceGuids max 200.");
        if (start is null && stop is null)
            throw new ArgumentException("waybillStartTime or waybillStopTime is required.");

        var resp = await PostAsync<BatchWaybillResp>(
            endpoint: "/api/data-api/elitechAccess/batchSetWaybill",
            body: new
            {
                keyId = _opt.KeyId,
                keySecret = _opt.KeySecret,
                deviceGuids = arr,
                waybillStartTime = start,
                waybillStopTime = stop
            },
            auth: true,
            ct: ct);

        return resp ?? new BatchWaybillResp { code = -1, message = "Empty response" };
    }
    public async Task<AlarmRecordResp> GetAlarmRecordAsync(
      string deviceGuid,
      int subUid,
      DateTimeOffset from,
      DateTimeOffset to,
      CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceGuid))
            throw new ArgumentException("deviceGuid is required", nameof(deviceGuid));

        if (from > to) (from, to) = (to, from);

        var resp = await PostAlarmAsync<AlarmRecordResp>(
            endpoint: "/api/data-api/elitechAccess/v2/getAlarmRecord",
            body: new
            {
                keyId = _alarmOpt.KeyId,
                keySecret = _alarmOpt.KeySecret,
                deviceGuid = deviceGuid.Trim(),
                subUid = subUid,
                startTime = from.ToUnixTimeSeconds(),
                endTime = to.ToUnixTimeSeconds()
            },
            auth: true,
            ct: ct);

        return resp ?? new AlarmRecordResp { code = -1, message = "Empty response" };
    }

    private async Task<T?> PostAlarmAsync<T>(string endpoint, object body, bool auth, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);

        if (auth)
        {
            var token = await GetTokenAsync(ct); // token dùng chung vẫn OK
            req.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
        }

        req.Content = new StringContent(
            JsonSerializer.Serialize(body, _jsonOpts),
            Encoding.UTF8,
            "application/json");

        using var res = await _httpAlarm.SendAsync(req, ct);
        var content = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)res.StatusCode} when calling (Alarm){endpoint}. Body: {content}");

        try
        {
            return JsonSerializer.Deserialize<T>(content, _jsonOpts);
        }
        catch (Exception ex)
        {
            throw new JsonException($"Deserialize failed for (Alarm){endpoint}. Body: {content}", ex);
        }
    }

}
