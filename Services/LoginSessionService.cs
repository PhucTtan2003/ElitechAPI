using Elitech.Data;
using Elitech.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;


namespace Elitech.Services
{
    public class LoginSessionService
    {
        private readonly MongoContext _ctx;
        private readonly IOptionsMonitor<SessionPolicyOptions> _opt;

        public LoginSessionService(MongoContext ctx, IOptionsMonitor<SessionPolicyOptions> opt)
        {
            _ctx = ctx;
            _opt = opt;
        }

        // ✅ ALWAYS read current config (appsettings reload -> ăn liền)
        private SessionPolicyOptions O => _opt.CurrentValue ?? new SessionPolicyOptions();

        // ===== Policy (từ appsettings) =====
        private TimeSpan IdleTimeout => TimeSpan.FromMinutes(Math.Max(1, O.IdleMinutes));
        private TimeSpan WarnBefore => TimeSpan.FromMinutes(Math.Max(1, O.WarnBeforeMinutes));
        private TimeSpan ResumeGraceWindow => TimeSpan.FromMinutes(Math.Max(0, O.ResumeGraceMinutes));
        private TimeSpan TouchThreshold => TimeSpan.FromSeconds(Math.Max(5, O.TouchThresholdSeconds));

        // =========================
        // CREATE / RESUME SESSION (LOGIN)
        // - 1 account chỉ 1 session active
        // - login lại trong grace window => resume 1 phiên
        // =========================
        public async Task<LoginSession> CreateOrResumeAsync(
            string accountId,
            string? ip,
            string? userAgent,
            CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            // 1) Khi login mới, tạo sessionId mới trước để ghi "ai đá ai"
            var newSid = Guid.NewGuid().ToString("N");

            // 2) Revoke tất cả session active của account (để "số 2 vào, số 1 văng")
            // + ghi thêm "bị ai đá" = session mới + ip/ua của người login mới
            var activeFilter = Builders<LoginSession>.Filter.And(
                Builders<LoginSession>.Filter.Eq(x => x.AccountId, accountId),
                Builders<LoginSession>.Filter.Eq(x => x.IsActive, true)
            );

            await _ctx.LoginSessions.UpdateManyAsync(
                activeFilter,
                Builders<LoginSession>.Update
                    .Set(x => x.IsActive, false)
                    .Set(x => x.RevokedAtUtc, now)
                    .Set(x => x.RevokedReason, "New login from another device")
                    .Set(x => x.RevokedBySessionId, newSid)
                    .Set(x => x.RevokedByIp, ip)
                    .Set(x => x.RevokedByUserAgent, userAgent),
                cancellationToken: ct);

            // 3) Resume session vừa logout trong grace window (vẫn tính 1 phiên)
            if (ResumeGraceWindow > TimeSpan.Zero)
            {
                var resumeFrom = now - ResumeGraceWindow;

                var resume = await _ctx.LoginSessions.Find(x =>
                        x.AccountId == accountId &&
                        x.IsActive == false &&
                        x.LogoutAtUtc != null &&
                        x.LogoutAtUtc >= resumeFrom &&
                        x.RevokedAtUtc == null
                    )
                    .SortByDescending(x => x.LogoutAtUtc)
                    .FirstOrDefaultAsync(ct);

                if (resume != null)
                {
                    await _ctx.LoginSessions.UpdateOneAsync(
                        x => x.SessionId == resume.SessionId,
                        Builders<LoginSession>.Update
                            .Set(x => x.IsActive, true)
                            .Set(x => x.LastActivityAtUtc, now)
                            .Set(x => x.Ip, ip)
                            .Set(x => x.UserAgent, userAgent)
                            .Set(x => x.LogoutAtUtc, (DateTime?)null),
                        cancellationToken: ct);

                    resume.IsActive = true;
                    resume.LastActivityAtUtc = now;
                    resume.Ip = ip;
                    resume.UserAgent = userAgent;
                    resume.LogoutAtUtc = null;
                    return resume;
                }
            }

            // 4) Create new session
            var s = new LoginSession
            {
                AccountId = accountId,
                SessionId = newSid,
                LoginAtUtc = now,
                LastActivityAtUtc = now,
                IsActive = true,
                Ip = ip,
                UserAgent = userAgent
            };

            await _ctx.LoginSessions.InsertOneAsync(s, cancellationToken: ct);
            return s;
        }

        // =========================
        // STATUS (KHÔNG TOUCH)
        // dùng cho cảnh báo trước X phút
        // =========================
        public async Task<(bool ok, SessionStatus status)> GetStatusAsync(
            string sessionId,
            CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            var s = await _ctx.LoginSessions.Find(x => x.SessionId == sessionId)
                .FirstOrDefaultAsync(ct);

            if (s == null)
                return (false, SessionStatus.Invalid("not_found", now));

            if (!s.IsActive)
                return (false, SessionStatus.Invalid("inactive", now));

            if (s.RevokedAtUtc != null)
                return (false, SessionStatus.Invalid("revoked", now, s.RevokedReason));

            var expiresAt = s.LastActivityAtUtc.Add(IdleTimeout);
            var remaining = expiresAt - now;

            return (true, new SessionStatus
            {
                ServerNowUtc = now,
                ExpiresAtUtc = expiresAt,
                RemainingSeconds = (int)Math.Floor(remaining.TotalSeconds),
                ShouldWarn = remaining <= WarnBefore && remaining > TimeSpan.Zero,
                ShouldLogout = remaining <= TimeSpan.Zero,
                Revoked = false,
                RevokedReason = null,

                // (optional) debug info
                IdleMinutes = (int)IdleTimeout.TotalMinutes,
                WarnBeforeMinutes = (int)WarnBefore.TotalMinutes
            });
        }

        // =========================
        // TOUCH (CÓ NGƯỠNG) - gọi khi user có tương tác thật
        // =========================
        public async Task TouchAsync(string sessionId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return;

            var now = DateTime.UtcNow;

            var s = await _ctx.LoginSessions.Find(x => x.SessionId == sessionId && x.IsActive)
                .FirstOrDefaultAsync(ct);

            if (s == null) return;
            if (s.RevokedAtUtc != null) return;

            // ✅ giảm ghi DB theo TouchThreshold (đọc live từ appsettings)
            if (now - s.LastActivityAtUtc < TouchThreshold)
                return;

            await _ctx.LoginSessions.UpdateOneAsync(
                x => x.Id == s.Id,
                Builders<LoginSession>.Update.Set(x => x.LastActivityAtUtc, now),
                cancellationToken: ct);
        }

        // =========================
        // VALIDATE (KHÔNG TOUCH) - middleware đá ra
        // =========================
        public async Task<bool> ValidateAsync(string sessionId, CancellationToken ct = default)
        {
            var (ok, status) = await GetStatusAsync(sessionId, ct);
            if (!ok) return false;

            // quá hạn => expire session
            if (status.ShouldLogout)
            {
                await ExpireBySessionIdAsync(sessionId, ct);
                return false;
            }

            return true;
        }

        // =========================
        // LOGOUT / REVOKE / EXPIRE
        // =========================
        public async Task LogoutBySessionIdAsync(string sessionId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return;

            var now = DateTime.UtcNow;

            await _ctx.LoginSessions.UpdateOneAsync(
                x => x.SessionId == sessionId && x.IsActive,
                Builders<LoginSession>.Update
                    .Set(x => x.IsActive, false)
                    .Set(x => x.LogoutAtUtc, now),
                cancellationToken: ct);
        }

        public async Task RevokeBySessionIdAsync(string sessionId, string reason, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return;

            var now = DateTime.UtcNow;

            await _ctx.LoginSessions.UpdateOneAsync(
                x => x.SessionId == sessionId && x.IsActive,
                Builders<LoginSession>.Update
                    .Set(x => x.IsActive, false)
                    .Set(x => x.RevokedAtUtc, now)
                    .Set(x => x.RevokedReason, reason),
                cancellationToken: ct);
        }

        public async Task ExpireBySessionIdAsync(string sessionId, CancellationToken ct = default)
        {
            // ✅ message cũng lấy theo IdleTimeout hiện tại
            await RevokeBySessionIdAsync(
                sessionId,
                $"Expired by idle timeout ({(int)IdleTimeout.TotalMinutes}m)",
                ct);
        }

    }
}
