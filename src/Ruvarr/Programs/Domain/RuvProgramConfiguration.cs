using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ruvarr.Programs.Domain;

internal sealed class RuvProgramConfiguration : IEntityTypeConfiguration<RuvProgram>
{
    public void Configure(EntityTypeBuilder<RuvProgram> builder)
    {
        builder.ToTable("programs");

        builder.Property<int>("id");
        builder.HasKey("id");

        builder.HasIndex(x => x.RuvId)
            .IsUnique();

        builder.Property(x => x.RuvId)
            .IsRequired();

        builder.Property(x => x.Channel)
            .HasMaxLength(64)
            .IsUnicode(true)
            .IsFixedLength(false)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(256)
            .IsUnicode(true)
            .IsFixedLength(false)
            .IsRequired();

        builder.Property(x => x.ForeignName)
            .HasMaxLength(256)
            .IsUnicode(true)
            .IsFixedLength(false);

        builder.Property(x => x.IsMonitored)
            .IsRequired();

        builder.Property(x => x.HasMissingEpisodes)
            .IsRequired();

        builder.HasMany(x => x.Episodes)
            .WithOne(x => x.Program)
            .HasForeignKey("program_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Series)
            .WithMany(x => x.Programs)
            .HasForeignKey("series_id");

        builder.HasOne(x => x.Movie)
            .WithMany(x => x.Programs)
            .HasForeignKey("movie_id");

        builder.Navigation(x => x.Episodes)
            .AutoInclude();

        builder.Navigation(x => x.Series)
            .AutoInclude();

        builder.Navigation(x => x.Movie)
            .AutoInclude();
    }
}