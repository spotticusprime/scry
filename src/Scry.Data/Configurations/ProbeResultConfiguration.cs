using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scry.Core;
using Scry.Data.Converters;

namespace Scry.Data.Configurations;

internal sealed class ProbeResultConfiguration : IEntityTypeConfiguration<ProbeResult>
{
    public void Configure(EntityTypeBuilder<ProbeResult> builder)
    {
        builder.ToTable("ProbeResults");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.WorkspaceId).IsRequired();
        builder.Property(r => r.ProbeId).IsRequired();
        builder.Property(r => r.Outcome).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.Message).HasMaxLength(4000);
        builder.Property(r => r.DurationMs).IsRequired();
        builder.Property(r => r.StartedAt).IsRequired();
        builder.Property(r => r.CompletedAt).IsRequired();

        builder.Property(r => r.Attributes)
            .HasConversion(new DictionaryJsonConverter())
            .Metadata.SetValueComparer(new DictionaryValueComparer());

        builder.HasOne<Probe>()
            .WithMany()
            .HasForeignKey(r => r.ProbeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.ProbeId, r.CompletedAt });
        builder.HasIndex(r => new { r.WorkspaceId, r.CompletedAt });
    }
}
