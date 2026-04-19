namespace Scry.Core;

public class AlertRule
{
    public AlertRule()
    {
        var now = DateTimeOffset.UtcNow;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid WorkspaceId { get; init; }
    public required string Name { get; set; }
    public required string Expression { get; set; }
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
    public bool Enabled { get; set; } = true;
    public TimeSpan For { get; set; } = TimeSpan.Zero;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}
