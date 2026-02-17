using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ruvarr.Downloads.Domain;

internal sealed class DownloadQueueItemConfiguration : IEntityTypeConfiguration<DownloadQueueItem>
{
    public void Configure(EntityTypeBuilder<DownloadQueueItem> builder)
    {
        builder.Property<int>("id");
        builder.HasKey("id");

        builder.Property(x => x.Episode)
            .IsRequired();

        builder.Property(x => x.Created)
            .IsRequired();

        builder.HasOne(x => x.Episode)
            .WithOne()
            .HasForeignKey("episode_id");
    }
}