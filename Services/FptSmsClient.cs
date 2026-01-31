using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Elitech.Services;

public class FptSmsOptions
{
    public string BaseUrl { get; set; } = "https://app.sms.fpt.net";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Scope { get; set; } = "send_brandname_otp";
    public string BrandName { get; set; } = "";
}

public class FptSmsClient
{
    private readonly HttpClient _http;
    private readonly FptSmsOptions _opt;

    private string? _accessToken; // raw token (không kèm "Bearer ")
    private DateTimeOffset _tokenGotAt;
    private int _expiresInSec = 0;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FptSmsClient(HttpClient http, IOptions<FptSmsOptions> opt)
    {
        _http = http;
        _opt = opt.Value;
        _http.BaseAddress = new Uri(_opt.BaseUrl.TrimEnd('/'));
    }

    private static string NewSessionId() => Guid.NewGuid().ToString("N");

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        // cache token
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            var age = DateTimeOffset.UtcNow - _tokenGotAt;
            if (_expiresInSec > 0 && age < TimeSpan.FromSeconds(Math.Max(30, _expiresInSec - 60)))
                return _accessToken!;
        }

        await _lock.WaitAsync(ct);
        try
        {
            // double check
            if (!string.IsNullOrWhiteSpace(_accessToken))
            {
                var age = DateTimeOffset.UtcNow - _tokenGotAt;
                if (_expiresInSec > 0 && age < TimeSpan.FromSeconds(Math.Max(30, _expiresInSec - 60)))
                    return _accessToken!;
            }

            var body = new
            {
                client_id = _opt.ClientId,
                client_secret = _opt.ClientSecret,
                scope = _opt.Scope,
                session_id = NewSessionId(),
                grant_type = "client_credentials"
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token");
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var res = await _http.SendAsync(req, ct);
            var json = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
                throw new HttpRequestException($"FPT token HTTP {(int)res.StatusCode}: {json}");

            using var doc = JsonDocument.Parse(json);
            _accessToken = doc.RootElement.GetProperty("access_token").GetString();
            _expiresInSec = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 86400;

            if (string.IsNullOrWhiteSpace(_accessToken))
                throw new InvalidOperationException($"FPT token empty: {json}");

            _tokenGotAt = DateTimeOffset.UtcNow;
            return _accessToken!;
        }
        finally
        {
            _lock.Release();
        }
    }

    // Gửi dạng "otp domestic" (message base64)
    public async Task<(bool ok, string raw)> SendDomesticAsync(
        string phone,
        string messagePlain,
        string? requestId,
        CancellationToken ct)
    {
        var token = await GetTokenAsync(ct);

        var msgB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(messagePlain));

        var body = new
        {
            access_token = token,
            session_id = NewSessionId(),
            BrandName = _opt.BrandName,
            Phone = phone,
            Message = msgB64,
            RequestId = requestId
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/push-brandname-otp");
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);

        return (res.IsSuccessStatusCode, raw);
    }
}
