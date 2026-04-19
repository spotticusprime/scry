using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Scry.Core;

namespace Scry.Data;

public class ScryDbContext : DbContext
{
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

        modelBuilder.Entity<Asset>().HasQueryFilter(a => CurrentWorkspaceId == null || a.WorkspaceId == CurrentWorkspaceId);
        modelBuilder.Entity<AssetRelationship>().HasQueryFilter(r => CurrentWorkspaceId == null || r.WorkspaceId == CurrentWorkspaceId);
        modelBuilder.Entity<Probe>().HasQueryFilter(p => CurrentWorkspaceId == null || p.WorkspaceId == CurrentWorkspaceId);
        modelBuilder.Entity<ProbeResult>().HasQueryFilter(r => CurrentWorkspaceId == null || r.WorkspaceId == CurrentWorkspaceId);
        modelBuilder.Entity<Job>().HasQueryFilter(j => CurrentWorkspaceId == null || j.WorkspaceId == CurrentWorkspaceId);
        modelBuilder.Entity<AlertRule>().HasQueryFilter(r => CurrentWorkspaceId == null || r.WorkspaceId == CurrentWorkspaceId);
        modelBuilder.Entity<AlertEvent>().HasQueryFilter(e => CurrentWorkspaceId == null || e.WorkspaceId == CurrentWorkspaceId);
        modelBuilder.Entity<MaintenanceWindow>().HasQueryFilter(m => CurrentWorkspaceId == null || m.WorkspaceId == CurrentWorkspaceId);
    }

    public override int SaveChanges()
    {
        StampTimestamps();
        return base.SaveChanges();
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
            if (entry.State != EntityState.Modified)
            {
                continue;
            }
            if (entry.Metadata.FindProperty(nameof(Workspace.UpdatedAt)) is null)
            {
                continue;
            }
            entry.Property(nameof(Workspace.UpdatedAt)).CurrentValue = now;
        }
    }
}
