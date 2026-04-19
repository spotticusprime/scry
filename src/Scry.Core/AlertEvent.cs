namespace Scry.Core;

public class AlertEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid WorkspaceId { get; init; }
    public required Guid AlertRuleId { get; init; }
    public required string Fingerprint { get; init; }
    public AlertState State { get; set; } = AlertState.Pending;
    public required AlertSeverity Severity { get; set; }
    public string? Summary { get; set; }
    public DateTimeOffset OpenedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset? LastNotifiedAt { get; set; }
    public Dictionary<string, string> Labels { get; init; } = new();
}
