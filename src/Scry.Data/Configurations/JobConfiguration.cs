using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scry.Core;

namespace Scry.Data.Configurations;

internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    private readonly bool _crossDatabase;

    // crossDatabase: true when Jobs live in a separate DB from Workspaces —
    // skips the FK that would require a cross-DB join.
    public JobConfiguration(bool crossDatabase = false)
    {
        _crossDatabase = crossDatabase;
    }

    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.WorkspaceId).IsRequired();
        builder.Property(j => j.Kind).IsRequired().HasMaxLength(100);
        builder.Property(j => j.Payload).IsRequired();
        builder.Property(j => j.Status).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(j => j.ClaimedBy).HasMaxLength(200);
        builder.Property(j => j.AttemptCount).IsRequired();
        builder.Property(j => j.MaxAttempts).IsRequired();
        builder.Property(j => j.LastError).HasMaxLength(4000);
        builder.Property(j => j.CreatedAt).IsRequired();
        builder.Property(j => j.UpdatedAt).IsRequired();

        if (!_crossDatabase)
        {
            builder.HasOne<Workspace>()
                .WithMany()
                .HasForeignKey(j => j.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        builder.HasIndex(j => new { j.WorkspaceId, j.Status, j.RunAfter });
        builder.HasIndex(j => new { j.WorkspaceId, j.Status, j.LeaseExpiresAt });
        builder.HasIndex(j => new { j.Status, j.RunAfter });
    }
}
