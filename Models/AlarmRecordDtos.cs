using System.Text.Json.Serialization;

namespace Elitech.Models;

public class GetAlarmRecordReq
{
    public string keyId { get; set; } = "";
    public string keySecret { get; set; } = "";
    public string deviceGuid { get; set; } = "";
    public int subUid { get; set; } = 0;
    public long startTime { get; set; }   // seconds
    public long endTime { get; set; }     // seconds
}

public class AlarmRecordItem
{
    public string? deviceGuid { get; set; }
    public int? subUid { get; set; }
    public string? deviceName { get; set; }
    public int? type { get; set; }
    public string? alarmName { get; set; }
    public long? alarmTimeStamp { get; set; }
    public string? alarmMessage { get; set; }
}

public class AlarmRecordResp
{
    public int code { get; set; }

    // tài liệu lúc thì dùng msg, lúc thì message
    public string? msg { get; set; }
    public string? message { get; set; }

    public object? error { get; set; }
    public string? time { get; set; }

    public List<AlarmRecordItem>? data { get; set; }
}
