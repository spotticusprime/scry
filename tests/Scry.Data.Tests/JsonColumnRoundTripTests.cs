using Microsoft.EntityFrameworkCore;
using Scry.Core;

namespace Scry.Data.Tests;

public class JsonColumnRoundTripTests
{
    [Fact]
    public async Task AssetAttributes_RoundTripThroughJson()
    {
        using var fixture = new SqliteTestFixture();
        var workspaceId = Guid.NewGuid();

        await using (var seed = fixture.CreateContext())
        {
            seed.Workspaces.Add(new Workspace { Id = workspaceId, Name = "ws" });
            seed.Assets.Add(new Asset
            {
                WorkspaceId = workspaceId,
                Name = "host1",
                Kind = AssetKind.Host,
                Attributes = new Dictionary<string, string>
                {
                    ["region"] = "us-east-1",
                    ["tier"] = "prod",
                },
            });
            await seed.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext(workspaceId);
        var asset = await read.Assets.SingleAsync();
        Assert.Equal("us-east-1", asset.Attributes["region"]);
        Assert.Equal("prod", asset.Attributes["tier"]);
    }

    [Fact]
    public async Task MaintenanceWindowAssetIds_RoundTripThroughJson()
    {
        using var fixture = new SqliteTestFixture();
        var workspaceId = Guid.NewGuid();
        var assetIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        await using (var seed = fixture.CreateContext())
        {
            seed.Workspaces.Add(new Workspace { Id = workspaceId, Name = "ws" });
            seed.MaintenanceWindows.Add(new MaintenanceWindow
            {
                WorkspaceId = workspaceId,
                Name = "weekend patch",
                StartsAt = DateTimeOffset.UtcNow,
                EndsAt = DateTimeOffset.UtcNow.AddHours(4),
                AssetIds = assetIds,
            });
            await seed.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext(workspaceId);
        var window = await read.MaintenanceWindows.SingleAsync();
        Assert.NotNull(window.AssetIds);
        Assert.Equal(assetIds, window.AssetIds);
    }

    [Fact]
    public async Task MaintenanceWindowAssetIds_NullPersistsAsNull()
    {
        using var fixture = new SqliteTestFixture();
        var workspaceId = Guid.NewGuid();

        await using (var seed = fixture.CreateContext())
        {
            seed.Workspaces.Add(new Workspace { Id = workspaceId, Name = "ws" });
            seed.MaintenanceWindows.Add(new MaintenanceWindow
            {
                WorkspaceId = workspaceId,
                Name = "all assets",
                StartsAt = DateTimeOffset.UtcNow,
                EndsAt = DateTimeOffset.UtcNow.AddHours(1),
                AssetIds = null,
            });
            await seed.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext(workspaceId);
        var window = await read.MaintenanceWindows.SingleAsync();
        Assert.Null(window.AssetIds);
    }
}
