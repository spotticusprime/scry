namespace Scry.Core;

public interface IJobQueue
{
    Task EnqueueAsync(Job job, CancellationToken ct = default);
    Task<Job?> ClaimNextAsync(Guid workspaceId, string workerId, TimeSpan leaseDuration, CancellationToken ct = default);
    Task CompleteAsync(Guid jobId, CancellationToken ct = default);
    Task FailAsync(Guid jobId, string error, TimeSpan retryDelay, CancellationToken ct = default);
}
