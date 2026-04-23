using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Scry.Core;

namespace Scry.Data;

internal sealed class JobReaper : BackgroundService
{
    private readonly IDbContextFactory<ScryJobDbContext> _factory;
    private readonly TimeSpan _interval;

    public JobReaper(IDbContextFactory<ScryJobDbContext> factory) : this(factory, TimeSpan.FromSeconds(30)) { }

    internal JobReaper(IDbContextFactory<ScryJobDbContext> factory, TimeSpan interval)
    {
        _factory = factory;
        _interval = interval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ReclaimStaleLeases(stoppingToken);
            await Task.Delay(_interval, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    internal async Task ReclaimStaleLeases(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;

        // Cap at 50 per tick: large batches hold the write lock for the full SaveChanges
        // duration and starve the claim path on busy instances.
        // Load only what's needed to branch Dead vs. Pending; full entity load is wasteful.
        var stale = await ctx.Jobs
            .IgnoreQueryFilters() // no CurrentWorkspaceId on internal contexts; reaper is cross-workspace by design
            .Where(j => j.Status == JobStatus.Claimed && j.LeaseExpiresAt < now)
            .Take(50)
            .Select(j => new { j.Id, j.AttemptCount, j.MaxAttempts })
            .ToListAsync(ct);

        foreach (var item in stale)
        {
            if (item.AttemptCount >= item.MaxAttempts)
            {
                // Re-check lease + status in the WHERE so a renewed lease between the
                // ToListAsync read and this update is a guaranteed no-op.
                await ctx.Jobs
                    .IgnoreQueryFilters()
                    .Where(j => j.Id == item.Id && j.Status == JobStatus.Claimed && j.LeaseExpiresAt < now)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(j => j.Status, JobStatus.Dead)
                        .SetProperty(j => j.ClaimedBy, (string?)null)
                        .SetProperty(j => j.ClaimedAt, (DateTimeOffset?)null)
                        .SetProperty(j => j.LeaseExpiresAt, (DateTimeOffset?)null)
                        .SetProperty(j => j.UpdatedAt, now), ct);
            }
            else
            {
                var runAfter = now + ExponentialBackoff(item.AttemptCount);
                await ctx.Jobs
                    .IgnoreQueryFilters()
                    .Where(j => j.Id == item.Id && j.Status == JobStatus.Claimed && j.LeaseExpiresAt < now)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(j => j.Status, JobStatus.Pending)
                        .SetProperty(j => j.RunAfter, runAfter)
                        .SetProperty(j => j.ClaimedBy, (string?)null)
                        .SetProperty(j => j.ClaimedAt, (DateTimeOffset?)null)
                        .SetProperty(j => j.LeaseExpiresAt, (DateTimeOffset?)null)
                        .SetProperty(j => j.UpdatedAt, now), ct);
            }
        }
    }

    private static TimeSpan ExponentialBackoff(int attempt) =>
        TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), 3600));
}
