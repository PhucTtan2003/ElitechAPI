namespace Elitech.Models
{
    public class AddDeviceResp
    {
        public int code { get; set; }
        public string? msg { get; set; }
        public string? message { get; set; }
        public object? error { get; set; }
        public string? time { get; set; }

        // ✅ FIX: cho phép null
        public int data { get; set; }
    }
}
