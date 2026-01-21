namespace Elitech.Models.HistoryData
{
    public class HistoryResp
    {
        public int code { get; set; }
        public string? message { get; set; }
        public string? msg { get; set; }
        public string? error { get; set; }
        public string? time { get; set; }
        public List<HistoryItem>? data { get; set; }
    }
}
