using Elitech.Models;

using Elitech.Models.DeviceInfo;
using Elitech.Models.HistoryData;
using Elitech.Models.RealtimeData;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Elitech.Services;

public class ElitechApiClient
{
    private readonly HttpClient _http;
    private readonly ElitechOptions _opt;

    // token cache
    private string? _cachedToken;
    private DateTimeOffset _tokenGotAt;

    // device guids cache
    private List<string>? _cachedDeviceGuids;
    private DateTimeOffset _deviceGuidsCachedAt;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ElitechApiClient(HttpClient http, ElitechOptions opt)
    {
        _http = http;
        _opt = opt;
        _http.BaseAddress = new Uri(_opt.BaseUrl);
    }

    // =========================
    // Token
    // =========================
    private async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(_cachedToken) &&
            DateTimeOffset.UtcNow - _tokenGotAt < TimeSpan.FromMinutes(30))
            return _cachedToken!;

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

        // data thường là chuỗi "bearer xxx"
        var token = payload.data?.Trim();
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(
                $"Cannot get token. code={payload.code}, msg={payload.msg ?? payload.message ?? payload.error}");

        // chuẩn hoá: luôn “Bearer ” viết hoa
        if (token.StartsWith("bearer ", StringComparison.OrdinalIgnoreCase))
            token = "Bearer " + token.Substring(7);
        else if (!token.StartsWith("Bearer ", StringComparison.Ordinal))
            token = "Bearer " + token;

        _cachedToken = token;
        _tokenGotAt = DateTimeOffset.UtcNow;
        return _cachedToken!;
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
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        using var res = await _http.SendAsync(req, ct);

        // ĐỪNG EnsureSuccess — để đọc body khi lỗi (500/502...)
        var content = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)res.StatusCode} when calling {endpoint}. Body: {content}");

        return JsonSerializer.Deserialize<T>(content, _jsonOpts);
    }

    // =========================
    // API #8 - Add Device
    // =========================
    public async Task<AddDeviceResp> AddDeviceAsync(IEnumerable<AddDeviceInfo> deviceInfos, CancellationToken ct = default)
    {
        var list = (deviceInfos ?? Enumerable.Empty<AddDeviceInfo>())
            .Where(x =>
                x is not null &&
                !string.IsNullOrWhiteSpace(x.deviceGuid) &&
                !string.IsNullOrWhiteSpace(x.deviceName))
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
    // API #7 - Get Device GUIDs (visible to account)
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
        long startSec = start.ToUnixTimeSeconds();
        long endSec = end.ToUnixTimeSeconds();

        // dùng chung PostAsync để đồng nhất error handling
        var resp = await PostAsync<HistoryResp>(
            "/api/data-api/elitechAccess/getHistoryData",
            new
            {
                keyId = _opt.KeyId,
                keySecret = _opt.KeySecret,
                deviceGuid,
                startTime = startSec,
                endTime = endSec
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
            cur = next.AddSeconds(1); // tránh overlap
        }

        var all = new List<HistoryItem>();

        foreach (var (s, e) in chunks)
        {
            ct.ThrowIfCancellationRequested();

            var st = s.ToUnixTimeSeconds();
            var et = e.ToUnixTimeSeconds();

            var resp = await PostAsync<HistoryResp>(
                "/api/data-api/elitechAccess/getHistoryData",
                new
                {
                    keyId = _opt.KeyId,
                    keySecret = _opt.KeySecret,
                    deviceGuid,
                    startTime = st,
                    endTime = et
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
                // nếu lỗi ở lát lớn → chia nhỏ hơn (đệ quy)
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
}
