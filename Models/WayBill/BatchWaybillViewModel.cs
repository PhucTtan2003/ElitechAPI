namespace Elitech.Models.Waybill
{
    public class BatchWaybillViewModel
    {
        public class BatchWaybillReq
        {
            public List<string> deviceGuids { get; set; } = new();

            // unix seconds (nullable: cho phép chỉ set start hoặc stop)
            public long? waybillStartTime { get; set; }
            public long? waybillStopTime { get; set; }
        }

        public class BatchWaybillResp
        {
            public int code { get; set; }
            public string? msg { get; set; }
            public string? message { get; set; }
            public string? error { get; set; }
            public string? time { get; set; }

            // doc của bạn đang trả data true/false
            public bool? data { get; set; }
        }
    }
}
