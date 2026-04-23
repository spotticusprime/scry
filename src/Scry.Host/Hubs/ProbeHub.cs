using Microsoft.AspNetCore.SignalR;

namespace Scry.Host.Hubs;

public class ProbeHub : Hub
{
    public Task JoinWorkspace(string workspaceId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"workspace-{workspaceId}");
}
