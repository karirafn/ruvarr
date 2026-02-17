using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ruvarr.Tvdb.Domain;

internal sealed class TvdbSeriesConfiguration : IEntityTypeConfiguration<TvdbSeries>
{
    public void Configure(EntityTypeBuilder<TvdbSeries> builder)
    {
        builder.ToTable("series");

        builder.Property<int>("id");
        builder.HasKey("id");

        builder.HasIndex(x => x.TvdbId)
            .IsUnique();

        builder.Property(x => x.TvdbId)
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsFixedLength(false);

        builder.Property(x => x.Type)
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsFixedLength(false);

        builder.Property(x => x.Name)
            .HasMaxLength(256)
            .IsUnicode(true)
            .IsFixedLength(false);

        builder.Metadata
            .FindNavigation(nameof(TvdbSeries.Programs))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
