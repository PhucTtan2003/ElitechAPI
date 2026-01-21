namespace Elitech.Models.SetParam
{
    public class SetParamReq
    {
        public string deviceGuid { get; set; } = "";

        // Optional
        public int? recordInterval { get; set; }
        public int? uploadInterval { get; set; }

        public string? tmpUpper { get; set; }
        public string? tmpLower { get; set; }
        public string? humUpper { get; set; }
        public string? humLower { get; set; }

        // ScheduledJourneyStart
        public long? waybillStartTime { get; set; } // unix seconds
        public string? waybillNum { get; set; }     // unique identifier
    }

    public class SetParamResp
    {
        public int code { get; set; }
        public string? msg { get; set; }
        public string? message { get; set; }
        public string? error { get; set; }
        public string? time { get; set; }
        public bool? data { get; set; }
    }
}