using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Elitech.Hubs
{
    [Authorize]
    public class ElitechAlarmHub : Hub
    {
        private static string DevGroup(string deviceGuid) => $"dev:{(deviceGuid ?? "").Trim()}";
        private static string UserGroup(string userId) => $"user:{(userId ?? "").Trim()}";
        private static string RoleGroup(string role) => $"role:{(role ?? "").Trim().ToLowerInvariant()}";

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
                await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

            var roles = Context.User?.FindAll(ClaimTypes.Role)
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToLowerInvariant())
                .Distinct()
                ?? Enumerable.Empty<string>();

            foreach (var role in roles)
                await Groups.AddToGroupAsync(Context.ConnectionId, RoleGroup(role));

            await base.OnConnectedAsync();
        }

        public Task JoinDevice(string deviceGuid)
        {
            deviceGuid = (deviceGuid ?? "").Trim();
            if (string.IsNullOrWhiteSpace(deviceGuid)) return Task.CompletedTask;
            return Groups.AddToGroupAsync(Context.ConnectionId, DevGroup(deviceGuid));
        }

        public Task LeaveDevice(string deviceGuid)
        {
            deviceGuid = (deviceGuid ?? "").Trim();
            if (string.IsNullOrWhiteSpace(deviceGuid)) return Task.CompletedTask;
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, DevGroup(deviceGuid));
        }

        // optional: nếu muốn client gọi lại cũng được, nhưng OnConnectedAsync đã join rồi
        public Task JoinMyRoles() => OnConnectedAsync();
    }
}
