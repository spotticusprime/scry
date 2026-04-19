using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scry.Core;

namespace Scry.Data.Configurations;

internal sealed class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.ToTable("AlertRules");
        builder.HasKey(r => r.Id);
        builder.HasAlternateKey(r => new { r.WorkspaceId, r.Id });

        builder.Property(r => r.WorkspaceId).IsRequired();
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Expression).IsRequired();
        builder.Property(r => r.Severity).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.For).IsRequired().HasColumnName("ForDuration");
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(r => r.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.WorkspaceId);
        builder.HasIndex(r => new { r.WorkspaceId, r.Enabled });
    }
}
