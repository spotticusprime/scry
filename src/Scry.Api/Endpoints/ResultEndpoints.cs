using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Scry.Data;

namespace Scry.Api.Endpoints;

internal static class ResultEndpoints
{
    internal static IEndpointRouteBuilder MapResultEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceId:guid}/results").WithTags("Results");

        // Latest result per probe.
        group.MapGet("/latest", async (Guid workspaceId, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var results = await ctx.ProbeResults
                .GroupBy(r => r.ProbeId)
                .Select(g => g.OrderByDescending(r => r.CompletedAt).First())
                .ToListAsync();
            return Results.Ok(results.Select(ToDto));
        });

        // Last N results for a specific probe.
        group.MapGet("/{probeId:guid}", async (Guid workspaceId, Guid probeId, int limit, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var take = Math.Clamp(limit == 0 ? 50 : limit, 1, 500);
            var results = await ctx.ProbeResults
                .Where(r => r.ProbeId == probeId)
                .OrderByDescending(r => r.CompletedAt)
                .Take(take)
                .ToListAsync();
            return Results.Ok(results.Select(ToDto));
        });

        return app;
    }

    private static object ToDto(Core.ProbeResult r) => new
    {
        r.Id,
        r.WorkspaceId,
        r.ProbeId,
        r.Outcome,
        r.Message,
        r.DurationMs,
        r.StartedAt,
        r.CompletedAt,
        r.Attributes,
    };
}
