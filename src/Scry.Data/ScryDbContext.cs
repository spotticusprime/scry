using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Scry.Core;

namespace Scry.Data;

public class ScryDbContext : DbContext
{
    private const string CreatedAtProperty = "CreatedAt";
    private const string UpdatedAtProperty = "UpdatedAt";

    private static readonly MethodInfo ApplyWorkspaceFilterMethod = typeof(ScryDbContext)
        .GetMethod(nameof(ApplyWorkspaceFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public ScryDbContext(DbContextOptions<ScryDbContext> options) : base(options)
    {
    }

    public Guid? CurrentWorkspaceId { get; set; }

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetRelationship> AssetRelationships => Set<AssetRelationship>();
    public DbSet<Probe> Probes => Set<Probe>();
    public DbSet<ProbeResult> ProbeResults => Set<ProbeResult>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();
    public DbSet<MaintenanceWindow> MaintenanceWindows => Set<MaintenanceWindow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType == typeof(Workspace))
            {
                continue;
            }
            if (entityType.FindProperty(nameof(Asset.WorkspaceId)) is null)
            {
                continue;
            }
            ApplyWorkspaceFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    private void ApplyWorkspaceFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            CurrentWorkspaceId == null || EF.Property<Guid>(e, nameof(Asset.WorkspaceId)) == CurrentWorkspaceId);
    }

    public override int SaveChanges()
    {
        StampTimestamps();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            var hasCreatedAt = entry.Metadata.FindProperty(CreatedAtProperty) is not null;
            var hasUpdatedAt = entry.Metadata.FindProperty(UpdatedAtProperty) is not null;

            if (entry.State == EntityState.Added)
            {
                if (hasCreatedAt)
                {
                    entry.Property(CreatedAtProperty).CurrentValue = now;
                }
                if (hasUpdatedAt)
                {
                    entry.Property(UpdatedAtProperty).CurrentValue = now;
                }
            }
            else if (entry.State == EntityState.Modified && hasUpdatedAt)
            {
                entry.Property(UpdatedAtProperty).CurrentValue = now;
            }
        }
    }
}
