namespace Scry.Core;

public class Job
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Kind { get; init; }
    public required string Payload { get; init; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public string? ClaimedBy { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public DateTimeOffset RunAfter { get; set; } = DateTimeOffset.UtcNow;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
