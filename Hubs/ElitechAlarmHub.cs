using Microsoft.AspNetCore.SignalR;

namespace Elitech.Hubs
{
    public class ElitechAlarmHub : Hub
    {
        // client gọi để join room theo deviceGuid (đỡ bắn cho tất cả)
        public async Task JoinDevice(string deviceGuid)
        {
            if (string.IsNullOrWhiteSpace(deviceGuid)) return;

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"dev:{deviceGuid.Trim()}");
        }

        public async Task LeaveDevice(string deviceGuid)
        {
            if (string.IsNullOrWhiteSpace(deviceGuid)) return;

            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                $"dev:{deviceGuid.Trim()}");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // nếu cần cleanup thì làm ở đây
            await base.OnDisconnectedAsync(exception);
        }
    }
}
