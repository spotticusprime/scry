using Microsoft.EntityFrameworkCore;
using Scry.Core;

namespace Scry.Data.Tests;

public class WorkspaceQueryFilterTests
{
    [Fact]
    public async Task QueryFilter_ScopesReadsToCurrentWorkspace()
    {
        using var fixture = new SqliteTestFixture();
        var workspaceA = Guid.NewGuid();
        var workspaceB = Guid.NewGuid();

        await using (var seed = fixture.CreateContext())
        {
            seed.Workspaces.AddRange(
                new Workspace { Id = workspaceA, Name = "A" },
                new Workspace { Id = workspaceB, Name = "B" });
            seed.Assets.AddRange(
                new Asset { WorkspaceId = workspaceA, Name = "a1", Kind = AssetKind.Host },
                new Asset { WorkspaceId = workspaceA, Name = "a2", Kind = AssetKind.Service },
                new Asset { WorkspaceId = workspaceB, Name = "b1", Kind = AssetKind.Host });
            await seed.SaveChangesAsync();
        }

        await using (var read = fixture.CreateContext(workspaceA))
        {
            var names = await read.Assets.Select(a => a.Name).OrderBy(n => n).ToListAsync();
            Assert.Equal(new[] { "a1", "a2" }, names);
        }

        await using (var read = fixture.CreateContext(workspaceB))
        {
            var names = await read.Assets.Select(a => a.Name).ToListAsync();
            Assert.Equal(new[] { "b1" }, names);
        }
    }

    [Fact]
    public async Task QueryFilter_WithoutWorkspace_ReturnsAllRows()
    {
        using var fixture = new SqliteTestFixture();
        var workspaceA = Guid.NewGuid();
        var workspaceB = Guid.NewGuid();

        await using (var seed = fixture.CreateContext())
        {
            seed.Workspaces.AddRange(
                new Workspace { Id = workspaceA, Name = "A" },
                new Workspace { Id = workspaceB, Name = "B" });
            seed.Assets.AddRange(
                new Asset { WorkspaceId = workspaceA, Name = "a1", Kind = AssetKind.Host },
                new Asset { WorkspaceId = workspaceB, Name = "b1", Kind = AssetKind.Host });
            await seed.SaveChangesAsync();
        }

        await using var unscoped = fixture.CreateContext();
        var count = await unscoped.Assets.CountAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task QueryFilter_IgnoreQueryFilters_BypassesScope()
    {
        using var fixture = new SqliteTestFixture();
        var workspaceA = Guid.NewGuid();
        var workspaceB = Guid.NewGuid();

        await using (var seed = fixture.CreateContext())
        {
            seed.Workspaces.AddRange(
                new Workspace { Id = workspaceA, Name = "A" },
                new Workspace { Id = workspaceB, Name = "B" });
            seed.Assets.AddRange(
                new Asset { WorkspaceId = workspaceA, Name = "a1", Kind = AssetKind.Host },
                new Asset { WorkspaceId = workspaceB, Name = "b1", Kind = AssetKind.Host });
            await seed.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext(workspaceA);
        var count = await read.Assets.IgnoreQueryFilters().CountAsync();
        Assert.Equal(2, count);
    }
}
