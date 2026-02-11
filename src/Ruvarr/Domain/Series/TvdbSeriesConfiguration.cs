using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ruvarr.Domain.Series;

internal sealed class TvdbSeriesConfiguration : IEntityTypeConfiguration<TvdbSeries>
{
    public void Configure(EntityTypeBuilder<TvdbSeries> builder)
    {
        builder.Property<int>("id");
        builder.HasKey("id");

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
    }
}
