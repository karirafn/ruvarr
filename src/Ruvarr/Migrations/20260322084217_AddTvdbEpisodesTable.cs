using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ruvarr.Migrations
{
    /// <inheritdoc />
    public partial class AddTvdbEpisodesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_episodes_program_id_tvdb_id",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "episode_number",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "is_missing",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "season_number",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "tvdb_id",
                table: "episodes");

            migrationBuilder.CreateTable(
                name: "tvdb_episodes",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    tvdb_id = table.Column<int>(type: "INTEGER", nullable: false),
                    season_number = table.Column<int>(type: "INTEGER", nullable: false),
                    episode_number = table.Column<int>(type: "INTEGER", nullable: false),
                    is_missing = table.Column<bool>(type: "INTEGER", nullable: false),
                    episode_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tvdb_episodes", x => x.id);
                    table.ForeignKey(
                        name: "fk_tvdb_episodes_episodes_episode_id",
                        column: x => x.episode_id,
                        principalTable: "episodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_episodes_program_id",
                table: "episodes",
                column: "program_id");

            migrationBuilder.CreateIndex(
                name: "ix_tvdb_episodes_episode_id_tvdb_id",
                table: "tvdb_episodes",
                columns: new[] { "episode_id", "tvdb_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tvdb_episodes");

            migrationBuilder.DropIndex(
                name: "ix_episodes_program_id",
                table: "episodes");

            migrationBuilder.AddColumn<int>(
                name: "episode_number",
                table: "episodes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_missing",
                table: "episodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "season_number",
                table: "episodes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tvdb_id",
                table: "episodes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_episodes_program_id_tvdb_id",
                table: "episodes",
                columns: new[] { "program_id", "tvdb_id" });
        }
    }
}
