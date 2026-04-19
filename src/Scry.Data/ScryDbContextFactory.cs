using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Scry.Data;

internal sealed class ScryDbContextFactory : IDesignTimeDbContextFactory<ScryDbContext>
{
    private const string ConnectionStringEnvVar = "SCRY_DESIGN_CONNECTION";
    private const string DefaultConnectionString = "Data Source=scry-design.db";

    public ScryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar) ?? DefaultConnectionString;
        var options = new DbContextOptionsBuilder<ScryDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new ScryDbContext(options);
    }
}
