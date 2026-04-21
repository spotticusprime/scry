using Microsoft.EntityFrameworkCore;

namespace Scry.Data.Tests;

internal sealed class FixtureDbContextFactory : IDbContextFactory<ScryDbContext>
{
    private readonly SqliteTestFixture _fixture;

    public FixtureDbContextFactory(SqliteTestFixture fixture) => _fixture = fixture;

    public ScryDbContext CreateDbContext() => _fixture.CreateContext();

    public Task<ScryDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
        Task.FromResult(_fixture.CreateContext());
}
