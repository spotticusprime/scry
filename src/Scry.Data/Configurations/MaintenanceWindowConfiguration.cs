using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scry.Core;
using Scry.Data.Converters;

namespace Scry.Data.Configurations;

internal sealed class MaintenanceWindowConfiguration : IEntityTypeConfiguration<MaintenanceWindow>
{
    public void Configure(EntityTypeBuilder<MaintenanceWindow> builder)
    {
        builder.ToTable("MaintenanceWindows");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.WorkspaceId).IsRequired();
        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);
        builder.Property(m => m.StartsAt).IsRequired();
        builder.Property(m => m.EndsAt).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();

        builder.Property(m => m.AssetIds)
            .HasConversion(new GuidListJsonConverter())
            .Metadata.SetValueComparer(new GuidListValueComparer());

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(m => m.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.WorkspaceId);
        builder.HasIndex(m => new { m.WorkspaceId, m.StartsAt, m.EndsAt });
    }
}
