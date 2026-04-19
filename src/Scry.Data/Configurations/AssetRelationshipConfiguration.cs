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

        builder.HasOne<Asset>()
            .WithMany()
            .HasForeignKey(r => r.SourceAssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Asset>()
            .WithMany()
            .HasForeignKey(r => r.TargetAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.WorkspaceId);
        builder.HasIndex(r => new { r.SourceAssetId, r.Kind });
        builder.HasIndex(r => new { r.TargetAssetId, r.Kind });
        builder.HasIndex(r => new { r.SourceAssetId, r.TargetAssetId, r.Kind }).IsUnique();
    }
}
