using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ruvarr.Domain.Ruv;

internal sealed class RuvEpisodeConfiguration : IEntityTypeConfiguration<RuvEpisode>
{
    public void Configure(EntityTypeBuilder<RuvEpisode> builder)
    {
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
    }
}