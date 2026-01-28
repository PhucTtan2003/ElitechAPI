using System.Text.Json.Serialization;

namespace Elitech.Models.DeviceInfo
{
    public class DeviceInfoItem
    {
        public string? deviceName { get; set; }
        public string? deviceGuid { get; set; }
        public int? subUid { get; set; }
        public string? deviceTypeName { get; set; }
        public long? expiredTime { get; set; }
        public int? smsCount { get; set; }
        public int? voiceCount { get; set; }
        public long? lastTime { get; set; }
        public string? sceneName { get; set; }

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? extra { get; set; }
    }
}
