namespace Scry.Core;

public class MaintenanceWindow
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid WorkspaceId { get; init; }
    public required string Name { get; set; }
    public required DateTimeOffset StartsAt { get; set; }
    public required DateTimeOffset EndsAt { get; set; }
    public IReadOnlyList<Guid>? AssetIds { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
