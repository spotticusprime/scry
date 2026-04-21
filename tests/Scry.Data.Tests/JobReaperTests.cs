using Microsoft.EntityFrameworkCore;
using Scry.Core;

namespace Scry.Data.Tests;

public class JobReaperTests
{
    private static Job StaleClaimed(Guid workspaceId, int attemptCount = 1, int maxAttempts = 5) => new()
    {
        WorkspaceId = workspaceId,
        Kind = "test",
        Payload = "{}",
        Status = JobStatus.Claimed,
        ClaimedBy = "worker-1",
        ClaimedAt = DateTimeOffset.UtcNow.AddHours(-2),
        LeaseExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
        AttemptCount = attemptCount,
        MaxAttempts = maxAttempts,
    };

    [Fact]
    public async Task ReclaimStaleLeases_ResetsStaledJobToPending()
    {
        using var fixture = new SqliteTestFixture();
        var wsId = Guid.NewGuid();

        await using var seed = fixture.CreateContext();
        seed.Workspaces.Add(new Workspace { Id = wsId, Name = "ws" });
        var job = StaleClaimed(wsId, attemptCount: 2, maxAttempts: 5);
        seed.Jobs.Add(job);
        await seed.SaveChangesAsync();

        var reaper = new JobReaper(new FixtureDbContextFactory(fixture), TimeSpan.Zero);
        await reaper.ReclaimStaleLeases();

        await using var ctx = fixture.CreateContext();
        var reloaded = await ctx.Jobs.SingleAsync(j => j.Id == job.Id);
        Assert.Equal(JobStatus.Pending, reloaded.Status);
        Assert.Null(reloaded.ClaimedBy);
        Assert.Null(reloaded.ClaimedAt);
        Assert.Null(reloaded.LeaseExpiresAt);
        Assert.True(reloaded.RunAfter > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ReclaimStaleLeases_MarksDeadWhenMaxAttemptsReached()
    {
        using var fixture = new SqliteTestFixture();
        var wsId = Guid.NewGuid();

        await using var seed = fixture.CreateContext();
        seed.Workspaces.Add(new Workspace { Id = wsId, Name = "ws" });
        var job = StaleClaimed(wsId, attemptCount: 5, maxAttempts: 5);
        seed.Jobs.Add(job);
        await seed.SaveChangesAsync();

        var reaper = new JobReaper(new FixtureDbContextFactory(fixture), TimeSpan.Zero);
        await reaper.ReclaimStaleLeases();

        await using var ctx = fixture.CreateContext();
        var reloaded = await ctx.Jobs.SingleAsync(j => j.Id == job.Id);
        Assert.Equal(JobStatus.Dead, reloaded.Status);
        Assert.Null(reloaded.ClaimedBy);
        Assert.Null(reloaded.ClaimedAt);
        Assert.Null(reloaded.LeaseExpiresAt);
    }

    [Fact]
    public async Task ReclaimStaleLeases_IgnoresJobWithActiveLease()
    {
        using var fixture = new SqliteTestFixture();
        var wsId = Guid.NewGuid();

        await using var seed = fixture.CreateContext();
        seed.Workspaces.Add(new Workspace { Id = wsId, Name = "ws" });
        seed.Jobs.Add(new Job
        {
            WorkspaceId = wsId,
            Kind = "test",
            Payload = "{}",
            Status = JobStatus.Claimed,
            ClaimedBy = "worker-1",
            ClaimedAt = DateTimeOffset.UtcNow,
            LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            AttemptCount = 1,
            MaxAttempts = 5,
        });
        await seed.SaveChangesAsync();

        var reaper = new JobReaper(new FixtureDbContextFactory(fixture), TimeSpan.Zero);
        await reaper.ReclaimStaleLeases();

        await using var ctx = fixture.CreateContext();
        var job = await ctx.Jobs.SingleAsync();
        Assert.Equal(JobStatus.Claimed, job.Status);
        Assert.Equal("worker-1", job.ClaimedBy);
    }

    [Fact]
    public async Task ReclaimStaleLeases_IgnoresPendingAndCompletedJobs()
    {
        using var fixture = new SqliteTestFixture();
        var wsId = Guid.NewGuid();

        await using var seed = fixture.CreateContext();
        seed.Workspaces.Add(new Workspace { Id = wsId, Name = "ws" });
        seed.Jobs.Add(new Job { WorkspaceId = wsId, Kind = "test", Payload = "{}", Status = JobStatus.Pending });
        seed.Jobs.Add(new Job { WorkspaceId = wsId, Kind = "test", Payload = "{}", Status = JobStatus.Completed });
        await seed.SaveChangesAsync();

        var reaper = new JobReaper(new FixtureDbContextFactory(fixture), TimeSpan.Zero);
        await reaper.ReclaimStaleLeases();

        await using var ctx = fixture.CreateContext();
        var jobs = await ctx.Jobs.ToListAsync();
        Assert.Contains(jobs, j => j.Status == JobStatus.Pending);
        Assert.Contains(jobs, j => j.Status == JobStatus.Completed);
    }
}
