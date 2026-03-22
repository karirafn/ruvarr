using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ruvarr.Programs.Domain;

internal sealed class TvdbEpisodeConfiguration : IEntityTypeConfiguration<TvdbEpisode>
{
    public void Configure(EntityTypeBuilder<TvdbEpisode> builder)
    {
        builder.ToTable("tvdb_episodes");

        builder.Property<int>("id");
        builder.HasKey("id");

        builder.HasIndex("episode_id", nameof(TvdbEpisode.TvdbId))
            .IsUnique();
    }
}
