using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Scry.Core;
using Scry.Data;

namespace Scry.Api.Endpoints;

internal static class TopologyEndpoints
{
    internal static IEndpointRouteBuilder MapTopologyEndpoints(this IEndpointRouteBuilder app)
    {
        var assets = app.MapGroup("/workspaces/{workspaceId:guid}/assets").WithTags("Topology");

        assets.MapGet("/", async (Guid workspaceId, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var list = await ctx.Assets.OrderBy(a => a.Name).ToListAsync();
            return Results.Ok(list.Select(ToAssetDto));
        });

        assets.MapPost("/", async (Guid workspaceId, CreateAssetRequest req, ScryDbContext ctx) =>
        {
            if (!Enum.TryParse<AssetKind>(req.Kind, ignoreCase: true, out var kind))
            {
                kind = AssetKind.Unknown;
            }
            var asset = new Asset
            {
                WorkspaceId = workspaceId,
                Name = req.Name,
                Kind = kind,
                ExternalId = req.ExternalId,
                Provider = req.Provider,
            };
            if (req.Description is not null)
            {
                asset.Attributes["description"] = req.Description;
            }
            ctx.Assets.Add(asset);
            await ctx.SaveChangesAsync();
            return Results.Created($"/api/workspaces/{workspaceId}/assets/{asset.Id}", ToAssetDto(asset));
        });

        assets.MapPut("/{id:guid}", async (Guid workspaceId, Guid id, UpdateAssetRequest req, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var asset = await ctx.Assets.FirstOrDefaultAsync(a => a.Id == id);
            if (asset is null)
            {
                return Results.NotFound();
            }
            if (req.Name is not null) { asset.Name = req.Name; }
            if (req.Kind is not null && Enum.TryParse<AssetKind>(req.Kind, ignoreCase: true, out var kind))
            {
                asset.Kind = kind;
            }
            if (req.ExternalId is not null) { asset.ExternalId = req.ExternalId; }
            if (req.Description is not null) { asset.Attributes["description"] = req.Description; }
            await ctx.SaveChangesAsync();
            return Results.Ok(ToAssetDto(asset));
        });

        assets.MapDelete("/{id:guid}", async (Guid workspaceId, Guid id, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var asset = await ctx.Assets.FirstOrDefaultAsync(a => a.Id == id);
            if (asset is null)
            {
                return Results.NotFound();
            }
            // Delete inbound relationships explicitly — TargetAssetId FK is Restrict, not Cascade.
            await ctx.AssetRelationships
                .Where(r => r.WorkspaceId == workspaceId && r.TargetAssetId == id)
                .ExecuteDeleteAsync();
            ctx.Assets.Remove(asset);
            await ctx.SaveChangesAsync();
            return Results.NoContent();
        });

        // POST /assets/{id}/relationships — id is the source asset
        assets.MapPost("/{id:guid}/relationships", async (Guid workspaceId, Guid id, CreateRelationshipRequest req, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var sourceExists = await ctx.Assets.AnyAsync(a => a.Id == id);
            var targetExists = await ctx.Assets.AnyAsync(a => a.Id == req.TargetAssetId);
            if (!sourceExists || !targetExists)
            {
                return Results.NotFound();
            }
            if (!Enum.TryParse<RelationshipKind>(req.Kind, ignoreCase: true, out var kind))
            {
                kind = RelationshipKind.DependsOn;
            }
            var rel = new AssetRelationship
            {
                WorkspaceId = workspaceId,
                SourceAssetId = id,
                TargetAssetId = req.TargetAssetId,
                Kind = kind,
            };
            ctx.AssetRelationships.Add(rel);
            await ctx.SaveChangesAsync();
            return Results.Created($"/api/workspaces/{workspaceId}/assets/relationships/{rel.Id}", ToRelDto(rel));
        });

        assets.MapDelete("/relationships/{id:guid}", async (Guid workspaceId, Guid id, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var rel = await ctx.AssetRelationships.FirstOrDefaultAsync(r => r.Id == id);
            if (rel is null)
            {
                return Results.NotFound();
            }
            ctx.AssetRelationships.Remove(rel);
            await ctx.SaveChangesAsync();
            return Results.NoContent();
        });

        app.MapGroup("/workspaces/{workspaceId:guid}")
            .WithTags("Topology")
            .MapGet("/topology", async (Guid workspaceId, ScryDbContext ctx, IAssetHealthService assetHealthSvc) =>
            {
                ctx.CurrentWorkspaceId = workspaceId;

                var assetList = await ctx.Assets.ToListAsync();
                var snapshot = await assetHealthSvc.GetSnapshotAsync(workspaceId);
                var edges = await ctx.AssetRelationships.ToListAsync();

                var nodes = assetList.Select(a => new
                {
                    id = a.Id.ToString(),
                    name = a.Name,
                    kind = a.Kind.ToString(),
                    health = snapshot.AssetHealth.TryGetValue(a.Id, out var h) ? h?.ToString() : null,
                });

                var edgeDtos = edges.Select(e => new
                {
                    id = e.Id.ToString(),
                    source = e.SourceAssetId.ToString(),
                    target = e.TargetAssetId.ToString(),
                    kind = e.Kind.ToString(),
                });

                return Results.Ok(new { nodes, edges = edgeDtos });
            }).RequireAuthorization();

        return app;
    }

    private static object ToAssetDto(Asset a) => new
    {
        a.Id,
        a.WorkspaceId,
        a.Name,
        Kind = a.Kind.ToString(),
        a.ExternalId,
        a.Provider,
        a.Attributes,
        a.CreatedAt,
        a.UpdatedAt,
    };

    private static object ToRelDto(AssetRelationship r) => new
    {
        r.Id,
        r.WorkspaceId,
        r.SourceAssetId,
        r.TargetAssetId,
        Kind = r.Kind.ToString(),
        r.CreatedAt,
    };

    internal sealed record CreateAssetRequest(
        string Name, string Kind, string? ExternalId, string? Provider, string? Description);

    internal sealed record UpdateAssetRequest(
        string? Name, string? Kind, string? ExternalId, string? Description);

    internal sealed record CreateRelationshipRequest(
        Guid TargetAssetId, string Kind);
}
