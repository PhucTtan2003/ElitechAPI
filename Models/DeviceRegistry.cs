using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace Elitech.Models
{
    public class DeviceRegistry
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string DeviceGuid { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public bool IsActive { get; set; } = true;

        public string AddedBy { get; set; } = "admin";
        public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
