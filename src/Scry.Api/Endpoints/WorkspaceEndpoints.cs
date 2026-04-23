using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Scry.Core;
using Scry.Data;

namespace Scry.Api.Endpoints;

internal static class WorkspaceEndpoints
{
    internal static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/workspaces").WithTags("Workspaces");

        group.MapGet("/", async (ScryDbContext ctx) =>
        {
            var workspaces = await ctx.Workspaces.OrderBy(w => w.Name).ToListAsync();
            return Results.Ok(workspaces.Select(ToDto));
        });

        group.MapGet("/{id:guid}", async (Guid id, ScryDbContext ctx) =>
        {
            var w = await ctx.Workspaces.FindAsync(id);
            return w is null ? Results.NotFound() : Results.Ok(ToDto(w));
        });

        group.MapPost("/", async (CreateWorkspaceRequest req, ScryDbContext ctx) =>
        {
            var workspace = new Workspace { Name = req.Name, Description = req.Description };
            ctx.Workspaces.Add(workspace);
            await ctx.SaveChangesAsync();
            return Results.Created($"/api/workspaces/{workspace.Id}", ToDto(workspace));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateWorkspaceRequest req, ScryDbContext ctx) =>
        {
            var w = await ctx.Workspaces.FindAsync(id);
            if (w is null)
            {
                return Results.NotFound();
            }
            w.Name = req.Name;
            w.Description = req.Description;
            await ctx.SaveChangesAsync();
            return Results.Ok(ToDto(w));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ScryDbContext ctx, IDbContextFactory<ScryJobDbContext> jobDbFactory) =>
        {
            var w = await ctx.Workspaces.FindAsync(id);
            if (w is null)
            {
                return Results.NotFound();
            }

            // Delete workspace from Postgres (cascade removes Probes, ProbeResults, AlertRules).
            ctx.Workspaces.Remove(w);
            await ctx.SaveChangesAsync();

            // Purge orphaned jobs from MySQL — no cross-DB FK means we must do this explicitly.
            await using var jobCtx = await jobDbFactory.CreateDbContextAsync();
            await jobCtx.Jobs
                .Where(j => j.WorkspaceId == id)
                .ExecuteDeleteAsync();

            return Results.NoContent();
        });

        return app;
    }

    private static object ToDto(Workspace w) => new
    {
        w.Id,
        w.Name,
        w.Description,
        w.CreatedAt,
        w.UpdatedAt,
    };

    internal sealed record CreateWorkspaceRequest(string Name, string? Description);
    internal sealed record UpdateWorkspaceRequest(string Name, string? Description);
}
