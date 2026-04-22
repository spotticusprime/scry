using Scry.Core;

namespace Scry.Runner.Tests;

internal sealed class FakeJobQueue : IJobQueue
{
    private readonly Queue<Job> _pending = new();
    public List<(Guid JobId, string WorkerId)> Completed { get; } = [];
    public List<(Guid JobId, string WorkerId, string Error)> Failed { get; } = [];
    public List<Job> Enqueued { get; } = [];

    public void Enqueue(Job job) => _pending.Enqueue(job);

    public Task EnqueueAsync(Job job, CancellationToken ct = default)
    {
        Enqueued.Add(job);
        _pending.Enqueue(job);
        return Task.CompletedTask;
    }

    public Task<Job?> ClaimNextAsync(Guid workspaceId, string workerId, TimeSpan leaseDuration, CancellationToken ct = default)
        => Task.FromResult(_pending.TryDequeue(out var job) ? job : null);

    public Task<Job?> ClaimAnyAsync(string workerId, TimeSpan leaseDuration, CancellationToken ct = default)
        => Task.FromResult(_pending.TryDequeue(out var job) ? job : null);

    public Task RenewLeaseAsync(Guid jobId, string workerId, TimeSpan leaseDuration, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task CompleteAsync(Guid jobId, string workerId, CancellationToken ct = default)
    {
        Completed.Add((jobId, workerId));
        return Task.CompletedTask;
    }

    public Task FailAsync(Guid jobId, string workerId, string error, TimeSpan retryDelay, CancellationToken ct = default)
    {
        Failed.Add((jobId, workerId, error));
        return Task.CompletedTask;
    }
}
