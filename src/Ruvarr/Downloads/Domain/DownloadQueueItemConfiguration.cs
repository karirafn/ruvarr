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
            .HasColumnName("failure_reason");
    }
}