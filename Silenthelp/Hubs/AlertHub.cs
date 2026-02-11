using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SilentHelp.Hubs
{
    [Authorize]
    public class AlertHub : Hub
    {
        // Parents join a group for their user ID so we can push alerts to them
        public async Task JoinParentGroup(string parentId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"parent_{parentId}");
        }

        public async Task LeaveParentGroup(string parentId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"parent_{parentId}");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
