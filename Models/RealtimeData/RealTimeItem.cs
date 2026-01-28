namespace Elitech.Models.RealtimeData
{
    public class RealTimeItem
    {
        public string? deviceGuid { get; set; }
        public int? subUid { get; set; }
        public string? deviceName { get; set; }
        public string? tmp1 { get; set; }
        public string? tmp2 { get; set; }
        public string? tmp3 { get; set; }
        public string? tmp4 { get; set; }
        public string? hum1 { get; set; }
        public string? hum2 { get; set; }
        public string? lux1 { get; set; }
        public string? power { get; set; }
        public string? signal { get; set; }
        public string? position { get; set; }       // "lng,lat"
        public string? address { get; set; }
        public long? lastSessionTime { get; set; }  // Unix seconds
        public long? lastAddressTime { get; set; }  // Unix seconds
        public bool? alarmState { get; set; }
        public bool? warnState { get; set; }
        public string? waybillStart { get; set; }   // "yyyy-MM-dd HH:mm:ss" (nếu có)
        public string? waybillEnd { get; set; }
        public string? waybillState { get; set; }   // "0" | "1" | "2" | "3"
    }
}
