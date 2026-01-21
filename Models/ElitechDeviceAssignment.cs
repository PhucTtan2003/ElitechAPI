using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace Elitech.Models
{
    public class ElitechDeviceAssignment
    {
        [BsonId] public ObjectId Id { get; set; }

        [BsonElement("userId")] public string UserId { get; set; } = "";
        [BsonElement("deviceGuid")] public string DeviceGuid { get; set; } = "";
        [BsonElement("deviceName")] public string? DeviceName { get; set; }

        [BsonElement("assignedAtUtc")] public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
