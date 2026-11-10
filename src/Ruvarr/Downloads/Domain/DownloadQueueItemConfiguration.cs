using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ruvarr.Downloads.Domain;

internal sealed class DownloadQueueItemConfiguration : IEntityTypeConfiguration<DownloadQueueItem>
{
    public void Configure(EntityTypeBuilder<DownloadQueueItem> builder)
    {
        builder.ToTable("download_queue");

        builder.Property<int>("id");
        builder.HasKey("id");

        builder.Property<int>("episode_id");

        // Declared explicitly because EF drops its conventional foreign-key index once another index
        // starts with the same column, and the filtered index below does not cover Complete rows.
        builder.HasIndex("episode_id");

        // An episode has at most one unfinished item, so Retry and Dismiss can resolve it by episode alone.
        builder.HasIndex(["episode_id"], "ix_download_queue_episode_id_unfinished")
            .IsUnique()
            .HasFilter("\"status\" <> 'Complete'")
            .HasDatabaseName("ix_download_queue_episode_id_unfinished");

        builder.Property(x => x.Created)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.FileName)
            .HasColumnName("file_name");

        builder.Property(x => x.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.NextRetryAt)
            .HasColumnName("next_retry_at");

        builder.Property(x => x.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(512);
    }
}