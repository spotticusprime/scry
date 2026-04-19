using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scry.Core;
using Scry.Data.Converters;

namespace Scry.Data.Configurations;

internal sealed class AlertEventConfiguration : IEntityTypeConfiguration<AlertEvent>
{
    public void Configure(EntityTypeBuilder<AlertEvent> builder)
    {
        builder.ToTable("AlertEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.WorkspaceId).IsRequired();
        builder.Property(e => e.AlertRuleId).IsRequired();
        builder.Property(e => e.Fingerprint).IsRequired().HasMaxLength(128);
        builder.Property(e => e.State).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.Severity).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.Summary).HasMaxLength(4000);
        builder.Property(e => e.OpenedAt).IsRequired();

        builder.Property(e => e.Labels)
            .HasConversion(new DictionaryJsonConverter())
            .Metadata.SetValueComparer(new DictionaryValueComparer());

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AlertRule>()
            .WithMany()
            .HasPrincipalKey(r => new { r.WorkspaceId, r.Id })
            .HasForeignKey(e => new { e.WorkspaceId, e.AlertRuleId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.WorkspaceId);
        builder.HasIndex(e => new { e.WorkspaceId, e.AlertRuleId, e.State });
        builder.HasIndex(e => new { e.WorkspaceId, e.AlertRuleId, e.Fingerprint, e.State });
    }
}
