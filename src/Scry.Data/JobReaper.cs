using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Scry.Core;

namespace Scry.Data;

internal sealed class JobReaper : BackgroundService
{
    private readonly IDbContextFactory<ScryDbContext> _factory;
    private readonly TimeSpan _interval;

    public JobReaper(IDbContextFactory<ScryDbContext> factory) : this(factory, TimeSpan.FromSeconds(30)) { }

    internal JobReaper(IDbContextFactory<ScryDbContext> factory, TimeSpan interval)
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
        // Cap at 50 per tick: a single SaveChangesAsync holds a write lock; loading all stale
        // jobs at once would starve the claim path on busy instances.
        var stale = await ctx.Jobs
            .IgnoreQueryFilters() // no CurrentWorkspaceId on internal contexts; reaper is cross-workspace by design
            .Where(j => j.Status == JobStatus.Claimed && j.LeaseExpiresAt < now)
            .Take(50)
            .ToListAsync(ct);

        foreach (var job in stale)
        {
            job.ClaimedBy = null;
            job.ClaimedAt = null;
            job.LeaseExpiresAt = null;

            if (job.AttemptCount >= job.MaxAttempts)
            {
                job.Status = JobStatus.Dead;
            }
            else
            {
                job.Status = JobStatus.Pending;
                job.RunAfter = now + ExponentialBackoff(job.AttemptCount);
            }
        }
        await ctx.SaveChangesAsync(ct);
    }

    private static TimeSpan ExponentialBackoff(int attempt) =>
        TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), 3600));
}
