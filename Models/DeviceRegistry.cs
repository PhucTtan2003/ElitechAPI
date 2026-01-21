using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Elitech.Models
{
    public class DeviceRegistry
    {
        [BsonId]
        public ObjectId Id { get; set; }   // ✅ Mongo tự sinh khi insert

        public string DeviceGuid { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public bool IsActive { get; set; } = true;

        public string AddedBy { get; set; } = "admin";
        public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
