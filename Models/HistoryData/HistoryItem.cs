namespace Elitech.Models.HistoryData
{
    public class HistoryItem
    {
        public string? deviceGuid { get; set; }
        public int? subUid { get; set; }
        public string? tmp1 { get; set; }
        public string? tmp2 { get; set; }
        public string? tmp3 { get; set; }
        public string? tmp4 { get; set; }
        public string? hum1 { get; set; }
        public string? hum2 { get; set; }
        public string? lux1 { get; set; }
        public string? power { get; set; }
        public string? signal { get; set; }
        public string? position { get; set; }
        public string? address { get; set; }
        public long? monitorTime { get; set; }
    }
}
