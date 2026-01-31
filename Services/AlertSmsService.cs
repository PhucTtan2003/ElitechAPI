namespace Elitech.Services;

public class AlertSmsService
{
    private readonly AccountService _accounts;
    private readonly FptSmsClient _sms;
    private readonly ILogger<AlertSmsService> _logger;

    public AlertSmsService(AccountService accounts, FptSmsClient sms, ILogger<AlertSmsService> logger)
    {
        _accounts = accounts;
        _sms = sms;
        _logger = logger;
    }

    public async Task<(bool ok, string? reason)> SendToUserAsync(
        string userId,
        string message,
        string requestId,
        CancellationToken ct)
    {
        var phone = await _accounts.GetPhoneByUserIdAsync(userId);
        if (string.IsNullOrWhiteSpace(phone))
            return (false, "User has no phone");

        var (ok, raw) = await _sms.SendDomesticAsync(phone, message, requestId, ct);

        if (!ok)
            _logger.LogWarning("SMS failed userId={UserId}, phone={Phone}, raw={Raw}", userId, phone, raw);

        return ok ? (true, null) : (false, raw);
    }
}
