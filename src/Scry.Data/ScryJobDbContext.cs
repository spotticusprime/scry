using Microsoft.EntityFrameworkCore;
using Scry.Core;
using Scry.Data.Configurations;

namespace Scry.Data;

public class ScryJobDbContext : DbContext
{
    private const string UpdatedAtProperty = "UpdatedAt";
    private const string CreatedAtProperty = "CreatedAt";

    public ScryJobDbContext(DbContextOptions<ScryJobDbContext> options) : base(options) { }

    public DbSet<Job> Jobs => Set<Job>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new JobConfiguration(crossDatabase: true));
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Metadata.FindProperty(CreatedAtProperty) is not null)
                { entry.Property(CreatedAtProperty).CurrentValue = now; }
                if (entry.Metadata.FindProperty(UpdatedAtProperty) is not null)
                { entry.Property(UpdatedAtProperty).CurrentValue = now; }
            }
            else if (entry.State == EntityState.Modified
                     && entry.Metadata.FindProperty(UpdatedAtProperty) is not null)
            {
                entry.Property(UpdatedAtProperty).CurrentValue = now;
            }
        }
    }
}
