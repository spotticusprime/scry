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

    public async Task<Job?> ClaimAnyAsync(string workerId, TimeSpan leaseDuration, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;

        // Same BEGIN IMMEDIATE strategy as ClaimNextAsync; no workspace filter — dispatcher
        // processes jobs for all workspaces.
        await using var tx = await ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var job = await ctx.Jobs
            .IgnoreQueryFilters() // no CurrentWorkspaceId on internal contexts; WHERE handles scoping
            .Where(j => j.Status == JobStatus.Pending && j.RunAfter <= now)
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
        var now = DateTimeOffset.UtcNow;

        // Single UPDATE with ownership predicate — re-checked atomically at write time so a
        // post-reap call from the original worker is a guaranteed no-op rather than a race.
        await ctx.Jobs
            .IgnoreQueryFilters() // no CurrentWorkspaceId on internal contexts; WHERE handles scoping
            .Where(j => j.Id == jobId && j.Status == JobStatus.Claimed && j.ClaimedBy == workerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.LeaseExpiresAt, now + leaseDuration)
                .SetProperty(j => j.UpdatedAt, now), ct);
    }

    public async Task CompleteAsync(Guid jobId, string workerId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;

        // Single UPDATE with ownership predicate — re-checked atomically at write time.
        await ctx.Jobs
            .IgnoreQueryFilters() // no CurrentWorkspaceId on internal contexts; WHERE handles scoping
            .Where(j => j.Id == jobId && j.Status == JobStatus.Claimed && j.ClaimedBy == workerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Completed)
                .SetProperty(j => j.ClaimedBy, (string?)null)
                .SetProperty(j => j.ClaimedAt, (DateTimeOffset?)null)
                .SetProperty(j => j.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(j => j.UpdatedAt, now), ct);
    }

    public async Task FailAsync(Guid jobId, string workerId, string error, TimeSpan retryDelay, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var retryAfter = now + retryDelay;

        // Two conditional UPDATEs; exactly one matches (or neither if no longer owned by this
        // worker). Ownership + attempt threshold checked atomically at write time on each.
        await ctx.Jobs
            .IgnoreQueryFilters() // no CurrentWorkspaceId on internal contexts; WHERE handles scoping
            .Where(j => j.Id == jobId && j.Status == JobStatus.Claimed && j.ClaimedBy == workerId
                        && j.AttemptCount >= j.MaxAttempts)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Dead)
                .SetProperty(j => j.LastError, error)
                .SetProperty(j => j.ClaimedBy, (string?)null)
                .SetProperty(j => j.ClaimedAt, (DateTimeOffset?)null)
                .SetProperty(j => j.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(j => j.UpdatedAt, now), ct);

        await ctx.Jobs
            .IgnoreQueryFilters() // no CurrentWorkspaceId on internal contexts; WHERE handles scoping
            .Where(j => j.Id == jobId && j.Status == JobStatus.Claimed && j.ClaimedBy == workerId
                        && j.AttemptCount < j.MaxAttempts)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Pending)
                .SetProperty(j => j.RunAfter, retryAfter)
                .SetProperty(j => j.LastError, error)
                .SetProperty(j => j.ClaimedBy, (string?)null)
                .SetProperty(j => j.ClaimedAt, (DateTimeOffset?)null)
                .SetProperty(j => j.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(j => j.UpdatedAt, now), ct);
    }
}
