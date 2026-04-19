namespace Scry.Core;

public class Asset
{
    public Asset()
    {
        var now = DateTimeOffset.UtcNow;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid WorkspaceId { get; init; }
    public required string Name { get; set; }
    public required AssetKind Kind { get; set; }
    public string? ExternalId { get; set; }
    public string? Provider { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}
