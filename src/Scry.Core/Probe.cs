namespace Scry.Core;

public class Probe
{
    public Probe()
    {
        var now = DateTimeOffset.UtcNow;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid WorkspaceId { get; init; }
    public required string Name { get; set; }
    public required string Kind { get; set; }
    public required string Definition { get; set; }
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);
    public bool Enabled { get; set; } = true;
    public Guid? AssetId { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}
