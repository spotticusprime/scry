using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Scry.Core;
using Scry.Data.Converters;

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

    private static readonly DateTimeOffsetToTicksConverter DtoConverter = new();
    private static readonly NullableDateTimeOffsetToTicksConverter NullableDtoConverter = new();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // SQLite has no native DateTimeOffset type and cannot run < / > on the ISO-8601
        // text EF writes by default. Scoped to SQLite so a future Postgres provider gets
        // native timestamptz and doesn't end up with bigint columns full of ticks.
        if (Database.IsSqlite())
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset))
                    {
                        property.SetValueConverter(DtoConverter);
                    }
                    else if (property.ClrType == typeof(DateTimeOffset?))
                    {
                        property.SetValueConverter(NullableDtoConverter);
                    }
                }
            }
        }

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
