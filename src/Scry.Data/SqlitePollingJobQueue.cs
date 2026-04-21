using System.Data;
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

        // IsolationLevel.Serializable → BEGIN IMMEDIATE in Microsoft.Data.Sqlite: the write
        // lock is acquired at transaction start so two concurrent workers cannot read the same
        // pending row before either commits. The default DEFERRED allows that race.
        await using var tx = await ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var job = await ctx.Jobs
            .IgnoreQueryFilters() // no CurrentWorkspaceId on internal contexts; WHERE handles scoping
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

    public async Task RenewLeaseAsync(Guid jobId, string workerId, TimeSpan leaseDuration, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var job = await ctx.Jobs
            .IgnoreQueryFilters() // no CurrentWorkspaceId on internal contexts; WHERE handles scoping
            .SingleOrDefaultAsync(j => j.Id == jobId && j.Status == JobStatus.Claimed && j.ClaimedBy == workerId, ct);
        if (job is null)
        {
            return;
        }
        job.LeaseExpiresAt = DateTimeOffset.UtcNow + leaseDuration;
        await ctx.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(Guid jobId, string workerId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var job = await ctx.Jobs
            .IgnoreQueryFilters() // no CurrentWorkspaceId on internal contexts; WHERE handles scoping
            .SingleOrDefaultAsync(j => j.Id == jobId && j.Status == JobStatus.Claimed && j.ClaimedBy == workerId, ct);
        if (job is null)
        {
            return;
        }
        job.Status = JobStatus.Completed;
        job.ClaimedBy = null;
        job.ClaimedAt = null;
        job.LeaseExpiresAt = null;
        await ctx.SaveChangesAsync(ct);
    }

    public async Task FailAsync(Guid jobId, string workerId, string error, TimeSpan retryDelay, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var job = await ctx.Jobs
            .IgnoreQueryFilters() // no CurrentWorkspaceId on internal contexts; WHERE handles scoping
            .SingleOrDefaultAsync(j => j.Id == jobId && j.Status == JobStatus.Claimed && j.ClaimedBy == workerId, ct);
        if (job is null)
        {
            return;
        }
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
