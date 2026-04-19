namespace Scry.Core;

public class Asset
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid WorkspaceId { get; init; }
    public required string Name { get; set; }
    public required AssetKind Kind { get; set; }
    public string? ExternalId { get; set; }
    public string? Provider { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
