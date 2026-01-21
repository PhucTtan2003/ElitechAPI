namespace Elitech.Models
{
    public class DeviceGuidsResp
    {
        public int Code { get; set; }
        public string? Msg { get; set; }
        public string? Message { get; set; }
        public object? Error { get; set; }
        public string? Time { get; set; }
        public List<string>? Data { get; set; }
    }
}
