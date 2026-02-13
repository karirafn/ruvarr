using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ruvarr.Domain.Ruv;

internal sealed class RuvProgramConfiguration : IEntityTypeConfiguration<RuvProgram>
{
    public void Configure(EntityTypeBuilder<RuvProgram> builder)
    {
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

        builder.HasMany(x => x.Episodes)
            .WithOne(x => x.Program)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Episodes)
            .AutoInclude();
    }
}