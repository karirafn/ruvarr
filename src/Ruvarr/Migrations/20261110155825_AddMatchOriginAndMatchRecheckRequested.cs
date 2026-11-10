using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ruvarr.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchOriginAndMatchRecheckRequested : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "match_recheck_requested",
                table: "programs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "match_origin",
                table: "episodes",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            // Only manual matching produces multi-link matches; earlier single-link manual matches cannot be told
            // apart from automatic ones and are backfilled as Automatic.
            migrationBuilder.Sql(
                "UPDATE episodes SET match_origin = CASE " +
                "WHEN (SELECT COUNT(*) FROM tvdb_episodes t WHERE t.episode_id = episodes.id) > 1 THEN 'Manual' " +
                "ELSE 'Automatic' END " +
                "WHERE EXISTS (SELECT 1 FROM tvdb_episodes t WHERE t.episode_id = episodes.id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "match_recheck_requested",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "match_origin",
                table: "episodes");
        }
    }
}
