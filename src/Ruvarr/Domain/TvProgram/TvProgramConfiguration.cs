using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ruvarr.Domain.TvProgram;

internal sealed class TvProgramConfiguration : IEntityTypeConfiguration<TvProgram>
{
    public void Configure(EntityTypeBuilder<TvProgram> builder)
    {
        builder.Property<int>("id");
        builder.HasKey("id");

        builder.Property(x => x.RuvId)
            .IsRequired();

        builder.Property(x => x.RuvChannel)
            .HasMaxLength(64)
            .IsUnicode(true)
            .IsFixedLength(false)
            .IsRequired();

        builder.Property(x => x.RuvName)
            .HasMaxLength(256)
            .IsUnicode(true)
            .IsFixedLength(false)
            .IsRequired();

        builder.Property(x => x.RuvForeignName)
            .HasMaxLength(256)
            .IsUnicode(true)
            .IsFixedLength(false);

        builder.Property(x => x.TvdbId)
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsFixedLength(false);

        builder.Property(x => x.TvdbType)
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsFixedLength(false);

        builder.Property(x => x.TvdbName)
            .HasMaxLength(256)
            .IsUnicode(true)
            .IsFixedLength(false);
    }
}