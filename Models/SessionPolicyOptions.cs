namespace Elitech.Models
{
    public class SessionPolicyOptions
    {
        // idle session (phút) - không thao tác thật
        public int IdleMinutes { get; set; } = 60;

        // cảnh báo trước khi hết hạn (phút)
        public int WarnBeforeMinutes { get; set; } = 10;

        // logout -> login lại trong bao lâu thì tính là 1 phiên (phút)
        public int ResumeGraceMinutes { get; set; } = 10;

        // ngưỡng touch để giảm ghi DB (giây)
        public int TouchThresholdSeconds { get; set; } = 120;
    }
}
