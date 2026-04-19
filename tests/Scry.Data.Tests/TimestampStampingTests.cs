using Microsoft.EntityFrameworkCore;
using Scry.Core;

namespace Scry.Data.Tests;

public class TimestampStampingTests
{
    [Fact]
    public async Task SaveChanges_UpdatesUpdatedAtOnModifiedEntity()
    {
        using var fixture = new SqliteTestFixture();
        var workspaceId = Guid.NewGuid();

        DateTimeOffset originalUpdatedAt;
        await using (var seed = fixture.CreateContext())
        {
            var ws = new Workspace { Id = workspaceId, Name = "ws" };
            seed.Workspaces.Add(ws);
            await seed.SaveChangesAsync();
            originalUpdatedAt = ws.UpdatedAt;
        }

        await Task.Delay(10);

        await using var edit = fixture.CreateContext();
        var loaded = await edit.Workspaces.SingleAsync(w => w.Id == workspaceId);
        loaded.Name = "renamed";
        await edit.SaveChangesAsync();

        Assert.True(loaded.UpdatedAt > originalUpdatedAt);
        Assert.Equal("renamed", loaded.Name);
    }

    [Fact]
    public async Task SaveChanges_DoesNotMutateCreatedAtOnUpdate()
    {
        using var fixture = new SqliteTestFixture();
        var workspaceId = Guid.NewGuid();

        DateTimeOffset originalCreatedAt;
        await using (var seed = fixture.CreateContext())
        {
            var ws = new Workspace { Id = workspaceId, Name = "ws" };
            seed.Workspaces.Add(ws);
            await seed.SaveChangesAsync();
            originalCreatedAt = ws.CreatedAt;
        }

        await using var edit = fixture.CreateContext();
        var loaded = await edit.Workspaces.SingleAsync(w => w.Id == workspaceId);
        loaded.Name = "renamed";
        await edit.SaveChangesAsync();

        Assert.Equal(originalCreatedAt, loaded.CreatedAt);
    }
}
