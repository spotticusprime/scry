using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Scry.Data;

internal sealed class ScryDbContextFactory : IDesignTimeDbContextFactory<ScryDbContext>
{
    public ScryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ScryDbContext>()
            .UseSqlite("Data Source=scry-design.db")
            .Options;

        return new ScryDbContext(options);
    }
}
