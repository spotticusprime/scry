using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Scry.Data.Tests;

internal sealed class SqliteTestFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ScryDbContext> _options;

    public SqliteTestFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ScryDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.Migrate();
    }

    public ScryDbContext CreateContext(Guid? workspaceId = null)
    {
        return new ScryDbContext(_options) { CurrentWorkspaceId = workspaceId };
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
