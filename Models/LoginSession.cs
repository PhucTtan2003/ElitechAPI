using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Elitech.Models
{
    public class LoginSession
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = default!;

        // ===== CORE =====

        [BsonElement("accountId")]
        public string AccountId { get; set; } = "";

        // Guid "N" – dùng để gắn claim "sid"
        [BsonElement("sessionId")]
        public string SessionId { get; set; } = "";

        [BsonElement("loginAtUtc")]
        public DateTime LoginAtUtc { get; set; } = DateTime.UtcNow;

        [BsonElement("lastActivityAtUtc")]
        public DateTime LastActivityAtUtc { get; set; } = DateTime.UtcNow;

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        // ===== NETWORK / DEVICE =====

        [BsonElement("ip")]
        public string? Ip { get; set; }

        [BsonElement("userAgent")]
        public string? UserAgent { get; set; }

        // ===== SESSION LIFECYCLE (THÊM MỚI) =====

        // Logout bình thường (user bấm Logout)
        [BsonElement("logoutAtUtc")]
        public DateTime? LogoutAtUtc { get; set; }

        // Bị đá do login nơi khác
        [BsonElement("revokedAtUtc")]
        public DateTime? RevokedAtUtc { get; set; }

        [BsonElement("revokedReason")]
        public string? RevokedReason { get; set; }

        [BsonElement("revokedBySessionId")]
        public string? RevokedBySessionId { get; set; }

        [BsonElement("revokedByIp")]
        public string? RevokedByIp { get; set; }

        [BsonElement("revokedByUserAgent")]
        public string? RevokedByUserAgent { get; set; }

    }
}
