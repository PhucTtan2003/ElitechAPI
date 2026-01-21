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

    public string? UserId { get; set; } = default!;
    public string DeviceGuid { get; set; } = default!;
    public string? DeviceName { get; set; }
    // 4 nhiệt, 2 ẩm

    public Range[] TempRanges { get; set; } = new Range[]

    {

        new (), new (), new (), new ()

    };
public Range[] HumRanges
{ get; set; } = new Range[]

{
    new(), new()
    };
// tuỳ bạn dùng sau

public int DebounceHits { get; set; } = 2;
public int CooldownSeconds { get; set; } = 180;
public bool Enabled { get; set; } = true;
public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

}
