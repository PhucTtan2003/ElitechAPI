namespace Elitech.Models
{
    public class ApiErrorDto
    {
        public string errorId { get; set; } = Guid.NewGuid().ToString("N");
        public string message { get; set; } = "Internal Server Error";
        public string? detail { get; set; }          // stacktrace / exception message (dev)
        public string? upstreamBody { get; set; }    // body Elitech trả về (nếu có)
        public int? upstreamStatus { get; set; }     // 500/502...
    }
}
