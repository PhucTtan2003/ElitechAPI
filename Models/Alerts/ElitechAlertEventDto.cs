namespace Elitech.Models.Alerts
{
    public class ElitechAlertEventDto
    {
        public string Id { get; set; } = "";
        public string DeviceGuid { get; set; } = "";
        public string? DeviceName { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string Reasons { get; set; } = "";
        public string Level { get; set; } = "";
        public bool IsRead { get; set; }
    }
}
