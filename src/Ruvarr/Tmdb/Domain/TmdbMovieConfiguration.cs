using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Ruvarr.Tvdb.Domain;

namespace Ruvarr.Tmdb.Domain;

internal sealed class TmdbMovieConfiguration : IEntityTypeConfiguration<TmdbMovie>
{
    public void Configure(EntityTypeBuilder<TmdbMovie> builder)
    {
        builder.ToTable("movies");

        builder.Property<int>("id");
        builder.HasKey("id");

        builder.HasIndex(x => x.TmdbId)
            .IsUnique();

        builder.Property(x => x.TmdbId)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(256)
            .IsUnicode(true)
            .IsFixedLength(false);

        builder.HasMany(x => x.Programs)
            .WithOne(x => x.Movie);

        builder.Metadata
            .FindNavigation(nameof(TvdbSeries.Programs))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}