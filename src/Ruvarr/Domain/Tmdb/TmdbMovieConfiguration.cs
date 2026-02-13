using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Ruvarr.Domain.Tvdb;

namespace Ruvarr.Domain.Tmdb;

internal sealed class TmdbMovieConfiguration : IEntityTypeConfiguration<TmdbMovie>
{
    public void Configure(EntityTypeBuilder<TmdbMovie> builder)
    {
        builder.Property<int>("id");
        builder.HasKey("id");

        builder.HasIndex(x => x.TmdbId)
            .IsUnique();

        builder.Property(x => x.TmdbId)
            .IsRequired();

        builder.Property(x => x.TmdbName)
            .HasMaxLength(256)
            .IsUnicode(true)
            .IsFixedLength(false);

        builder.Metadata
            .FindNavigation(nameof(TvdbSeries.Programs))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}