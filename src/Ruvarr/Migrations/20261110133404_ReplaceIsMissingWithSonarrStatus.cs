using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ruvarr.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceIsMissingWithSonarrStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sonarr_status",
                table: "tvdb_episodes",
                type: "TEXT",
                nullable: false,
                defaultValue: "NotTracked");

            // Carrying Missing over keeps the first sync from raising EpisodeMissingEvent (and re-queuing a
            // download) for every episode that was already missing. Whether Sonarr has a file is unknown here,
            // so the rest start as NotTracked until the first sync reads Sonarr.
            migrationBuilder.Sql(
                "UPDATE tvdb_episodes SET sonarr_status = CASE WHEN is_missing = 1 THEN 'Missing' ELSE 'NotTracked' END;");

            migrationBuilder.DropColumn(
                name: "is_missing",
                table: "tvdb_episodes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_missing",
                table: "tvdb_episodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE tvdb_episodes SET is_missing = CASE WHEN sonarr_status = 'Missing' THEN 1 ELSE 0 END;");

            migrationBuilder.DropColumn(
                name: "sonarr_status",
                table: "tvdb_episodes");
        }
    }
}
