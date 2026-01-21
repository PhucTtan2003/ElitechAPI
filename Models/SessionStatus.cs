namespace Elitech.Models
{
    public class SessionStatus
    {
        public DateTime ServerNowUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public int RemainingSeconds { get; set; }

        public bool ShouldWarn { get; set; }
        public bool ShouldLogout { get; set; }

        public bool Revoked { get; set; }
        public string? RevokedReason { get; set; }

        // ===== optional debug / display =====
        public int IdleMinutes { get; set; }
        public int WarnBeforeMinutes { get; set; }

        // ===== helpers =====
        public static SessionStatus Invalid(
            string reason,
            DateTime now,
            string? detail = null)
        {
            return new SessionStatus
            {
                ServerNowUtc = now,
                ExpiresAtUtc = now,
                RemainingSeconds = 0,
                ShouldWarn = false,
                ShouldLogout = true,
                Revoked = reason == "revoked",
                RevokedReason = detail ?? reason
            };
        }
    }
}
