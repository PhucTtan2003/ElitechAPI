namespace Elitech.Models.DeviceInfo
{
    public class DeviceInfoResp
    {
        public int? code { get; set; }
        public string? msg { get; set; }
        public string? message { get; set; }
        public List<DeviceInfoItem>? data { get; set; }
        public object? error { get; set; }
    }
}
