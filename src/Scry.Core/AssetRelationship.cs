namespace Scry.Core;

public class AssetRelationship
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid WorkspaceId { get; init; }
    public required Guid SourceAssetId { get; init; }
    public required Guid TargetAssetId { get; init; }
    public required RelationshipKind Kind { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
