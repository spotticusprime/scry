using Microsoft.AspNetCore.SignalR;
using Scry.Core;

namespace Scry.Host.Hubs;

internal sealed class SignalRProbeResultPublisher(IHubContext<ProbeHub> hub) : IProbeResultPublisher
{
    public Task PublishAsync(ProbeResult result, CancellationToken ct = default)
    {
        var group = $"workspace-{result.WorkspaceId}";
        return hub.Clients.Group(group).SendAsync(
            "ProbeResult",
            new
            {
                probeId = result.ProbeId,
                workspaceId = result.WorkspaceId,
                outcome = result.Outcome.ToString(),
                message = result.Message,
                completedAt = result.CompletedAt,
                durationMs = result.DurationMs,
            },
            ct);
    }
}
