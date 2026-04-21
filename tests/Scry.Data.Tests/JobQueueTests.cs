using Microsoft.EntityFrameworkCore;
using Scry.Core;

namespace Scry.Data.Tests;

public class JobQueueTests
{
    private static Job NewJob(Guid workspaceId, DateTimeOffset? runAfter = null) => new()
    {
        WorkspaceId = workspaceId,
        Kind = "test",
        Payload = "{}",
        RunAfter = runAfter ?? DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task EnqueueAsync_PersistsJobAsPending()
    {
        using var fixture = new SqliteTestFixture();
        var queue = new SqlitePollingJobQueue(new FixtureDbContextFactory(fixture));
        var wsId = Guid.NewGuid();

        await using var seed = fixture.CreateContext();
        seed.Workspaces.Add(new Workspace { Id = wsId, Name = "ws" });
        await seed.SaveChangesAsync();

        var job = NewJob(wsId);
        await queue.EnqueueAsync(job);

        await using var ctx = fixture.CreateContext();
        var persisted = await ctx.Jobs.SingleAsync(j => j.Id == job.Id);
        Assert.Equal(JobStatus.Pending, persisted.Status);
        Assert.Equal("test", persisted.Kind);
    }

    [Fact]
    public async Task ClaimNextAsync_ReturnsNullWhenNoPendingJobs()
    {
        using var fixture = new SqliteTestFixture();
        var queue = new SqlitePollingJobQueue(new FixtureDbContextFactory(fixture));
        var wsId = Guid.NewGuid();

        await using var seed = fixture.CreateContext();
        seed.Workspaces.Add(new Workspace { Id = wsId, Name = "ws" });
        await seed.SaveChangesAsync();

        var claimed = await queue.ClaimNextAsync(wsId, "worker-1", TimeSpan.FromMinutes(5));
        Assert.Null(claimed);
    }

    [Fact]
    public async Task ClaimNextAsync_ClaimsOldestEligibleJob()
    {
        using var fixture = new SqliteTestFixture();
        var queue = new SqlitePollingJobQueue(new FixtureDbContextFactory(fixture));
        var wsId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var seed = fixture.CreateContext();
        seed.Workspaces.Add(new Workspace { Id = wsId, Name = "ws" });
        var older = NewJob(wsId, now.AddMinutes(-2));
        var newer = NewJob(wsId, now.AddMinutes(-1));
        seed.Jobs.AddRange(older, newer);
        await seed.SaveChangesAsync();

        var claimed = await queue.ClaimNextAsync(wsId, "worker-1", TimeSpan.FromMinutes(5));

        Assert.NotNull(claimed);
        Assert.Equal(older.Id, claimed.Id);
        Assert.Equal(JobStatus.Claimed, claimed.Status);
        Assert.Equal("worker-1", claimed.ClaimedBy);
        Assert.NotNull(claimed.LeaseExpiresAt);
        Assert.Equal(1, claimed.AttemptCount);
    }

    [Fact]
    public async Task ClaimNextAsync_RespectsRunAfter()
    {
        using var fixture = new SqliteTestFixture();
        var queue = new SqlitePollingJobQueue(new FixtureDbContextFactory(fixture));
        var wsId = Guid.NewGuid();

        await using var seed = fixture.CreateContext();
        seed.Workspaces.Add(new Workspace { Id = wsId, Name = "ws" });
        seed.Jobs.Add(NewJob(wsId, DateTimeOffset.UtcNow.AddHours(1)));
        await seed.SaveChangesAsync();

        var claimed = await queue.ClaimNextAsync(wsId, "worker-1", TimeSpan.FromMinutes(5));
        Assert.Null(claimed);
    }

    [Fact]
    public async Task ClaimNextAsync_DoesNotClaimJobFromOtherWorkspace()
    {
        using var fixture = new SqliteTestFixture();
        var queue = new SqlitePollingJobQueue(new FixtureDbContextFactory(fixture));
        var wsA = Guid.NewGuid();
        var wsB = Guid.NewGuid();

        await using var seed = fixture.CreateContext();
        seed.Workspaces.Add(new Workspace { Id = wsA, Name = "ws-a" });
        seed.Workspaces.Add(new Workspace { Id = wsB, Name = "ws-b" });
        seed.Jobs.Add(NewJob(wsA));
        await seed.SaveChangesAsync();

        var claimed = await queue.ClaimNextAsync(wsB, "worker-1", TimeSpan.FromMinutes(5));
        Assert.Null(claimed);
    }

    [Fact]
    public async Task CompleteAsync_MarksJobCompleted()
    {
        using var fixture = new SqliteTestFixture();
        var queue = new SqlitePollingJobQueue(new FixtureDbContextFactory(fixture));
        var wsId = Guid.NewGuid();

        await using var seed = fixture.CreateContext();
        seed.Workspaces.Add(new Workspace { Id = wsId, Name = "ws" });
        seed.Jobs.Add(NewJob(wsId));
        await seed.SaveChangesAsync();

        var claimed = await queue.ClaimNextAsync(wsId, "worker-1", TimeSpan.FromMinutes(5));
        Assert.NotNull(claimed);

        await queue.CompleteAsync(claimed.Id);

        await using var ctx = fixture.CreateContext();
        var job = await ctx.Jobs.SingleAsync(j => j.Id == claimed.Id);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Null(job.ClaimedBy);
        Assert.Null(job.LeaseExpiresAt);
    }

    [Fact]
    public async Task FailAsync_ResetsToPendingWhenAttemptsRemain()
    {
        using var fixture = new SqliteTestFixture();
        var queue = new SqlitePollingJobQueue(new FixtureDbContextFactory(fixture));
        var wsId = Guid.NewGuid();

        await using var seed = fixture.CreateContext();
        seed.Workspaces.Add(new Workspace { Id = wsId, Name = "ws" });
        seed.Jobs.Add(NewJob(wsId));
        await seed.SaveChangesAsync();

        var claimed = await queue.ClaimNextAsync(wsId, "worker-1", TimeSpan.FromMinutes(5));
        Assert.NotNull(claimed);

        var retryDelay = TimeSpan.FromSeconds(10);
        var beforeFail = DateTimeOffset.UtcNow;
        await queue.FailAsync(claimed.Id, "boom", retryDelay);

        await using var ctx = fixture.CreateContext();
        var job = await ctx.Jobs.SingleAsync(j => j.Id == claimed.Id);
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal("boom", job.LastError);
        Assert.Null(job.ClaimedBy);
        Assert.True(job.RunAfter >= beforeFail + retryDelay);
    }

    [Fact]
    public async Task FailAsync_MarksDeadWhenMaxAttemptsExhausted()
    {
        using var fixture = new SqliteTestFixture();
        var queue = new SqlitePollingJobQueue(new FixtureDbContextFactory(fixture));
        var wsId = Guid.NewGuid();

        await using var seed = fixture.CreateContext();
        seed.Workspaces.Add(new Workspace { Id = wsId, Name = "ws" });
        // AttemptCount already at MaxAttempts — simulate a job that has been retried to the limit
        seed.Jobs.Add(new Job
        {
            WorkspaceId = wsId,
            Kind = "test",
            Payload = "{}",
            Status = JobStatus.Claimed,
            ClaimedBy = "worker-1",
            ClaimedAt = DateTimeOffset.UtcNow,
            LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            AttemptCount = 5,
            MaxAttempts = 5,
        });
        await seed.SaveChangesAsync();

        await using var lookupCtx = fixture.CreateContext();
        var jobId = (await lookupCtx.Jobs.SingleAsync()).Id;

        await queue.FailAsync(jobId, "final error", TimeSpan.FromMinutes(1));

        await using var ctx = fixture.CreateContext();
        var job = await ctx.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(JobStatus.Dead, job.Status);
        Assert.Equal("final error", job.LastError);
    }
}
