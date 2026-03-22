using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ruvarr.Programs.Domain;

internal sealed class RuvEpisodeConfiguration : IEntityTypeConfiguration<RuvEpisode>
{
    public void Configure(EntityTypeBuilder<RuvEpisode> builder)
    {
        builder.ToTable("episodes");

        builder.Property<int>("id");
        builder.HasKey("id");

        builder.HasIndex(x => x.RuvId)
            .IsUnique();

        builder.Property(x => x.RuvId)
            .HasMaxLength(6)
            .IsUnicode(false)
            .IsFixedLength(true)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(256)
            .IsUnicode(true)
            .IsFixedLength(false)
            .IsRequired();

        builder.Property(x => x.Uri)
            .HasConversion(
                x => x.ToString(),
                x => new Uri(x))
            .HasMaxLength(256)
            .IsUnicode(false)
            .IsFixedLength(false)
            .IsRequired();

        builder.Property<int>("program_id");

        builder.Property(x => x.Duration)
            .HasConversion(
                v => (long)v.TotalSeconds,
                v => TimeSpan.FromSeconds(v))
            .HasColumnName("duration_seconds");

        builder.Ignore(x => x.RuvUrl);

        builder.HasMany(x => x.TvdbEpisodes)
            .WithOne()
            .HasForeignKey("episode_id")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(RuvEpisode.TvdbEpisodes))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.DownloadQueueItems)
            .WithOne(x => x.Episode)
            .HasForeignKey("episode_id")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(RuvEpisode.DownloadQueueItems))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}