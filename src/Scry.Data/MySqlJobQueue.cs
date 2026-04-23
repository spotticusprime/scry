using Microsoft.EntityFrameworkCore;
using Scry.Core;

namespace Scry.Data;

internal sealed class MySqlJobQueue : IJobQueue
{
    private readonly IDbContextFactory<ScryJobDbContext> _factory;

    public MySqlJobQueue(IDbContextFactory<ScryJobDbContext> factory)
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
        await using var tx = await ctx.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var job = await ctx.Jobs
            .FromSqlRaw("""
                SELECT * FROM Jobs
                WHERE WorkspaceId = {0} AND Status = 'Pending' AND RunAfter <= {1}
                ORDER BY RunAfter
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """, workspaceId, now)
            .FirstOrDefaultAsync(ct);

        if (job is null) { await tx.RollbackAsync(ct); return null; }

        await ClaimJob(ctx, job, workerId, leaseDuration, now, ct);
        await tx.CommitAsync(ct);
        return job;
    }

    public async Task<Job?> ClaimAnyAsync(string workerId, TimeSpan leaseDuration, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        await using var tx = await ctx.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var job = await ctx.Jobs
            .FromSqlRaw("""
                SELECT * FROM Jobs
                WHERE Status = 'Pending' AND RunAfter <= {0}
                ORDER BY RunAfter
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """, now)
            .FirstOrDefaultAsync(ct);

        if (job is null) { await tx.RollbackAsync(ct); return null; }

        await ClaimJob(ctx, job, workerId, leaseDuration, now, ct);
        await tx.CommitAsync(ct);
        return job;
    }

    private static async Task ClaimJob(ScryJobDbContext ctx, Job job, string workerId, TimeSpan leaseDuration, DateTimeOffset now, CancellationToken ct)
    {
        job.Status = JobStatus.Claimed;
        job.ClaimedBy = workerId;
        job.ClaimedAt = now;
        job.LeaseExpiresAt = now + leaseDuration;
        job.AttemptCount++;
        await ctx.SaveChangesAsync(ct);
    }

    public async Task RenewLeaseAsync(Guid jobId, string workerId, TimeSpan leaseDuration, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        await ctx.Jobs
            .Where(j => j.Id == jobId && j.Status == JobStatus.Claimed && j.ClaimedBy == workerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.LeaseExpiresAt, now + leaseDuration)
                .SetProperty(j => j.UpdatedAt, now), ct);
    }

    public async Task CompleteAsync(Guid jobId, string workerId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        await ctx.Jobs
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
        await ctx.Jobs
            .Where(j => j.Id == jobId && j.Status == JobStatus.Claimed && j.ClaimedBy == workerId && j.AttemptCount >= j.MaxAttempts)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Dead)
                .SetProperty(j => j.LastError, error)
                .SetProperty(j => j.ClaimedBy, (string?)null)
                .SetProperty(j => j.ClaimedAt, (DateTimeOffset?)null)
                .SetProperty(j => j.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(j => j.UpdatedAt, now), ct);

        await ctx.Jobs
            .Where(j => j.Id == jobId && j.Status == JobStatus.Claimed && j.ClaimedBy == workerId && j.AttemptCount < j.MaxAttempts)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Pending)
                .SetProperty(j => j.RunAfter, now + retryDelay)
                .SetProperty(j => j.LastError, error)
                .SetProperty(j => j.ClaimedBy, (string?)null)
                .SetProperty(j => j.ClaimedAt, (DateTimeOffset?)null)
                .SetProperty(j => j.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(j => j.UpdatedAt, now), ct);
    }
}
