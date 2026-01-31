using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Elitech.Models.AlertRule;

public class ElitechAlertRuleViewModel
{
    public class Range
    {
        public double? Min { get; set; }
        public double? Max { get; set; }
    }

    [BsonId] public ObjectId Id { get; set; }

    /// <summary>
    /// Owner của rule:
    /// - USER: userId thật
    /// - GLOBAL: "__GLOBAL__"
    /// </summary>
    public string? UserId { get; set; } = default!;

    /// <summary>
    /// "USER" hoặc "GLOBAL"
    /// </summary>
    public string Scope { get; set; } = "USER";

    /// <summary>
    /// (Tuỳ chọn) Admin muốn set rule USER cho user khác.
    /// FE có thể gửi TargetUserId, server sẽ map sang UserId.
    /// </summary>
    [BsonIgnoreIfNull]
    public string? TargetUserId { get; set; }

    public string DeviceGuid { get; set; } = default!;
    public string? DeviceName { get; set; }

    // 4 nhiệt, 2 ẩm
    public Range[] TempRanges { get; set; } = new Range[] { new(), new(), new(), new() };
    public Range[] HumRanges { get; set; } = new Range[] { new(), new() };

    public int DebounceHits { get; set; } = 2;
    public int CooldownSeconds { get; set; } = 180;
    public bool Enabled { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
