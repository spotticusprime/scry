using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Scry.Data;

internal sealed class ScryJobDbContextFactory : IDesignTimeDbContextFactory<ScryJobDbContext>
{
    public ScryJobDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("SCRY_MYSQL_CONNECTION")
            ?? throw new InvalidOperationException("Set SCRY_MYSQL_CONNECTION for migrations.");
        var options = new DbContextOptionsBuilder<ScryJobDbContext>()
            .UseMySQL(cs)
            .Options;
        return new ScryJobDbContext(options);
    }
}
