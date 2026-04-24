using Microsoft.EntityFrameworkCore;
using Scry.Core;

namespace Scry.Data;

public sealed class AssetHealthService(IDbContextFactory<ScryDbContext> dbFactory) : IAssetHealthService
{
    public async Task<AssetHealthSnapshot> GetSnapshotAsync(Guid workspaceId)
    {
        await using var ctx = await dbFactory.CreateDbContextAsync();
        ctx.CurrentWorkspaceId = workspaceId;

        var probes = await ctx.Probes
            .Where(p => p.AssetId != null)
            .Select(p => new { p.Id, p.AssetId })
            .ToListAsync();

        var probeIds = probes.Select(p => p.Id).ToList();

        var latestResults = await ctx.ProbeResults
            .Where(r => probeIds.Contains(r.ProbeId))
            .GroupBy(r => r.ProbeId)
            .Select(g => g.OrderByDescending(r => r.CompletedAt).First())
            .ToListAsync();

        var resultByProbe = latestResults.ToDictionary(r => r.ProbeId, r => r.Outcome);
        var probeToAsset = probes
            .Where(p => p.AssetId.HasValue)
            .ToDictionary(p => p.Id, p => p.AssetId!.Value);

        var assetHealth = new Dictionary<Guid, ProbeOutcome?>();
        foreach (var probe in probes)
        {
            if (probe.AssetId is null) { continue; }
            var assetId = probe.AssetId.Value;
            if (!resultByProbe.TryGetValue(probe.Id, out var outcome)) { continue; }
            if (!assetHealth.TryGetValue(assetId, out var current) || current is null || IsWorse(outcome, current.Value))
            {
                assetHealth[assetId] = outcome;
            }
        }

        return new AssetHealthSnapshot(assetHealth, probeToAsset);
    }

    public bool IsWorse(ProbeOutcome incoming, ProbeOutcome current) =>
        Rank(incoming) > Rank(current);

    private static int Rank(ProbeOutcome o) => o switch
    {
        ProbeOutcome.Ok      => 0,
        ProbeOutcome.Unknown => 1,
        ProbeOutcome.Warn    => 2,
        ProbeOutcome.Crit    => 3,
        ProbeOutcome.Error   => 4, // Error = probe couldn't run; worst because state is unknown
        _                    => 0,
    };
}
