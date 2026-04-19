using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scry.Core;

namespace Scry.Data.Configurations;

internal sealed class ProbeConfiguration : IEntityTypeConfiguration<Probe>
{
    public void Configure(EntityTypeBuilder<Probe> builder)
    {
        builder.ToTable("Probes");
        builder.HasKey(p => p.Id);
        builder.HasAlternateKey(p => new { p.WorkspaceId, p.Id });

        builder.Property(p => p.WorkspaceId).IsRequired();
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Kind).IsRequired().HasMaxLength(64);
        builder.Property(p => p.Definition).IsRequired();
        builder.Property(p => p.Interval).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(p => p.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Asset>()
            .WithMany()
            .HasPrincipalKey(a => new { a.WorkspaceId, a.Id })
            .HasForeignKey(p => new { p.WorkspaceId, p.AssetId })
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.WorkspaceId);
        builder.HasIndex(p => new { p.WorkspaceId, p.Enabled });
        builder.HasIndex(p => new { p.WorkspaceId, p.AssetId });
    }
}
