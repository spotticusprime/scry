namespace Scry.Core;

public interface IAssetHealthService
{
    Task<AssetHealthSnapshot> GetSnapshotAsync(Guid workspaceId);
    bool IsWorse(ProbeOutcome incoming, ProbeOutcome current);
}

public sealed record AssetHealthSnapshot(
    Dictionary<Guid, ProbeOutcome?> AssetHealth,
    Dictionary<Guid, Guid> ProbeToAsset);
