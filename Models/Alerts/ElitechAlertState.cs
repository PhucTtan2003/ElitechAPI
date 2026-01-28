using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Elitech.Models.Alerts;

public class ElitechAlertState
{
    [BsonId] public ObjectId Id { get; set; }

    public string UserId { get; set; } = default!;
    public string DeviceGuid { get; set; } = default!;

    public int ConsecutiveBadHits { get; set; } = 0;
    public bool IsBad { get; set; } = false;

    public DateTime? LastAlertAtUtc { get; set; }
    public string? LastReasons { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public long? LastSampleTs { get; set; } // unix seconds

}
