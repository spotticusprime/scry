using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scry.Core;

namespace Scry.Data.Configurations;

internal sealed class AssetRelationshipConfiguration : IEntityTypeConfiguration<AssetRelationship>
{
    public void Configure(EntityTypeBuilder<AssetRelationship> builder)
    {
        builder.ToTable("AssetRelationships");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.WorkspaceId).IsRequired();
        builder.Property(r => r.SourceAssetId).IsRequired();
        builder.Property(r => r.TargetAssetId).IsRequired();
        builder.Property(r => r.Kind).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(r => r.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Asset>()
            .WithMany()
            .HasPrincipalKey(a => new { a.WorkspaceId, a.Id })
            .HasForeignKey(r => new { r.WorkspaceId, r.SourceAssetId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Asset>()
            .WithMany()
            .HasPrincipalKey(a => new { a.WorkspaceId, a.Id })
            .HasForeignKey(r => new { r.WorkspaceId, r.TargetAssetId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.WorkspaceId);
        builder.HasIndex(r => new { r.WorkspaceId, r.SourceAssetId, r.Kind });
        builder.HasIndex(r => new { r.WorkspaceId, r.TargetAssetId, r.Kind });
        builder.HasIndex(r => new { r.WorkspaceId, r.SourceAssetId, r.TargetAssetId, r.Kind }).IsUnique();
    }
}
