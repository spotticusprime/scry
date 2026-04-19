namespace Scry.Core;

public class ProbeResult
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid WorkspaceId { get; init; }
    public required Guid ProbeId { get; init; }
    public required ProbeOutcome Outcome { get; init; }
    public string? Message { get; init; }
    public long DurationMs { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public Dictionary<string, string> Attributes { get; init; } = new();
}
