using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Scry.Host.Hubs;

[Authorize]
public class ProbeHub : Hub
{
    public Task JoinWorkspace(string workspaceId)
    {
        if (!Guid.TryParse(workspaceId, out _))
        {
            throw new HubException("Invalid workspaceId.");
        }
        return Groups.AddToGroupAsync(Context.ConnectionId, $"workspace-{workspaceId}");
    }
}
