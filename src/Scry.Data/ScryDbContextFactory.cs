using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Scry.Data;

internal sealed class ScryDbContextFactory : IDesignTimeDbContextFactory<ScryDbContext>
{
    public ScryDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("SCRY_PGSQL_CONNECTION")
            ?? throw new InvalidOperationException("Set SCRY_PGSQL_CONNECTION for migrations.");
        var options = new DbContextOptionsBuilder<ScryDbContext>()
            .UseNpgsql(cs)
            .Options;
        return new ScryDbContext(options);
    }
}
