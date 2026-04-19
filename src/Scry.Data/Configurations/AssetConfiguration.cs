using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scry.Core;
using Scry.Data.Converters;

namespace Scry.Data.Configurations;

internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.WorkspaceId).IsRequired();
        builder.Property(a => a.Name).IsRequired().HasMaxLength(500);
        builder.Property(a => a.Kind).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.ExternalId).HasMaxLength(500);
        builder.Property(a => a.Provider).HasMaxLength(100);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();

        builder.Property(a => a.Attributes)
            .HasConversion(new DictionaryJsonConverter())
            .Metadata.SetValueComparer(new DictionaryValueComparer());

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(a => a.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.WorkspaceId);
        builder.HasIndex(a => new { a.WorkspaceId, a.Kind });
        builder.HasIndex(a => new { a.WorkspaceId, a.Provider, a.ExternalId })
            .IsUnique()
            .HasFilter("\"ExternalId\" IS NOT NULL");
    }
}
