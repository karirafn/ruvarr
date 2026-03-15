using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ruvarr.Programs.Domain;

internal sealed class TvdbSeriesConfiguration : IEntityTypeConfiguration<TvdbSeries>
{
    public void Configure(EntityTypeBuilder<TvdbSeries> builder)
    {
        builder.ToTable("series");

        builder.Property<int>("id");
        builder.HasKey("id");

        builder.HasIndex(x => x.TvdbId)
            .IsUnique();

        builder.Property(x => x.Name)
            .HasMaxLength(256)
            .IsUnicode(true)
            .IsFixedLength(false);

        builder.Property(x => x.Slug)
            .HasMaxLength(128)
            .IsUnicode(false)
            .IsFixedLength(false);

        builder.Metadata
            .FindNavigation(nameof(TvdbSeries.Programs))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}