using Microsoft.EntityFrameworkCore;
using Scry.Core;

namespace Scry.Data;

internal sealed class SqlitePollingJobQueue : IJobQueue
{
    private readonly IDbContextFactory<ScryDbContext> _factory;

    public SqlitePollingJobQueue(IDbContextFactory<ScryDbContext> factory)
    {
        _factory = factory;
    }

    public async Task EnqueueAsync(Job job, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        ctx.Jobs.Add(job);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<Job?> ClaimNextAsync(Guid workspaceId, string workerId, TimeSpan leaseDuration, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        await using var tx = await ctx.Database.BeginTransactionAsync(ct);

        var job = await ctx.Jobs
            .IgnoreQueryFilters()
            .Where(j => j.WorkspaceId == workspaceId && j.Status == JobStatus.Pending && j.RunAfter <= now)
            .OrderBy(j => j.RunAfter)
            .FirstOrDefaultAsync(ct);

        if (job is null)
        {
            await tx.RollbackAsync(ct);
            return null;
        }

        job.Status = JobStatus.Claimed;
        job.ClaimedBy = workerId;
        job.ClaimedAt = now;
        job.LeaseExpiresAt = now + leaseDuration;
        job.AttemptCount++;
        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return job;
    }

    public async Task CompleteAsync(Guid jobId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var job = await ctx.Jobs.IgnoreQueryFilters().SingleAsync(j => j.Id == jobId, ct);
        job.Status = JobStatus.Completed;
        job.ClaimedBy = null;
        job.ClaimedAt = null;
        job.LeaseExpiresAt = null;
        await ctx.SaveChangesAsync(ct);
    }

    public async Task FailAsync(Guid jobId, string error, TimeSpan retryDelay, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var job = await ctx.Jobs.IgnoreQueryFilters().SingleAsync(j => j.Id == jobId, ct);
        job.LastError = error;
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
            job.RunAfter = now + retryDelay;
        }
        await ctx.SaveChangesAsync(ct);
    }
}
