using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Elitech.Models.Alerts;

public class ElitechAlertEvent
{
    [BsonId] public ObjectId Id { get; set; }

    public string UserId { get; set; } = default!;
    public string DeviceGuid { get; set; } = default!;
    public string? DeviceName { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    // snapshot values (optional)
    public double? Tmp1 { get; set; }
    public double? Tmp2 { get; set; }
    public double? Tmp3 { get; set; }
    public double? Tmp4 { get; set; }
    public double? Hum1 { get; set; }
    public double? Hum2 { get; set; }

    public string Reasons { get; set; } = ""; // "TMP1_HIGH,HUM2_LOW"
    public string Level { get; set; } = "ALARM"; // ALARM/OK (tuỳ bạn mở rộng)
    public bool IsRead { get; set; } = false;
}
