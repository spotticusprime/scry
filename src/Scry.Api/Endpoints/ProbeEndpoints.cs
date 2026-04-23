using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Scry.Core;
using Scry.Data;
using Scry.Probes;

namespace Scry.Api.Endpoints;

internal static class ProbeEndpoints
{
    internal static IEndpointRouteBuilder MapProbeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceId:guid}/probes").WithTags("Probes");

        group.MapGet("/", async (Guid workspaceId, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var probes = await ctx.Probes.OrderBy(p => p.Name).ToListAsync();
            return Results.Ok(probes.Select(ToDto));
        });

        group.MapGet("/{id:guid}", async (Guid workspaceId, Guid id, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var p = await ctx.Probes.FirstOrDefaultAsync(p => p.Id == id);
            return p is null ? Results.NotFound() : Results.Ok(ToDto(p));
        });

        group.MapPost("/", async (Guid workspaceId, CreateProbeRequest req, ScryDbContext ctx) =>
        {
            var probe = new Probe
            {
                WorkspaceId = workspaceId,
                Name = req.Name,
                Kind = req.Kind,
                Definition = req.Definition,
                Interval = req.Interval ?? TimeSpan.FromMinutes(5),
            };
            ctx.Probes.Add(probe);
            // Seed the first job to start the recurring probe loop.
            ctx.Jobs.Add(ScryProbesExtensions.CreateInitialProbeJob(probe));
            await ctx.SaveChangesAsync();
            return Results.Created($"/api/workspaces/{workspaceId}/probes/{probe.Id}", ToDto(probe));
        });

        group.MapPut("/{id:guid}", async (Guid workspaceId, Guid id, UpdateProbeRequest req, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var p = await ctx.Probes.FirstOrDefaultAsync(p => p.Id == id);
            if (p is null)
            {
                return Results.NotFound();
            }
            p.Name = req.Name ?? p.Name;
            p.Definition = req.Definition ?? p.Definition;
            p.Interval = req.Interval ?? p.Interval;
            p.Enabled = req.Enabled ?? p.Enabled;
            await ctx.SaveChangesAsync();
            return Results.Ok(ToDto(p));
        });

        group.MapDelete("/{id:guid}", async (Guid workspaceId, Guid id, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var p = await ctx.Probes.FirstOrDefaultAsync(p => p.Id == id);
            if (p is null)
            {
                return Results.NotFound();
            }
            // Disabling is the "stop" signal; the job loop will terminate naturally.
            p.Enabled = false;
            await ctx.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    private static object ToDto(Probe p) => new
    {
        p.Id,
        p.WorkspaceId,
        p.Name,
        p.Kind,
        p.Definition,
        p.Interval,
        p.Enabled,
        p.CreatedAt,
        p.UpdatedAt,
    };

    internal sealed record CreateProbeRequest(
        string Name, string Kind, string Definition, TimeSpan? Interval);

    internal sealed record UpdateProbeRequest(
        string? Name, string? Definition, TimeSpan? Interval, bool? Enabled);
}
