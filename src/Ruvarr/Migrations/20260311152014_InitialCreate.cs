using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ruvarr.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "download_queue",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    downloaded = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_download_queue", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "movies",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    tmdb_id = table.Column<int>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "series",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    tvdb_id = table.Column<string>(type: "TEXT", unicode: false, maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_series", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "programs",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ruv_id = table.Column<int>(type: "INTEGER", nullable: false),
                    channel = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    foreign_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    has_multiple_episodes = table.Column<bool>(type: "INTEGER", nullable: false),
                    created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    matched = table.Column<DateTime>(type: "TEXT", nullable: true),
                    next_lookup = table.Column<DateTime>(type: "TEXT", nullable: true),
                    lookup_count = table.Column<int>(type: "INTEGER", nullable: false),
                    series_id = table.Column<int>(type: "INTEGER", nullable: true),
                    movie_id = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_programs", x => x.id);
                    table.ForeignKey(
                        name: "fk_programs_movies_movie_id",
                        column: x => x.movie_id,
                        principalTable: "movies",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_programs_series_series_id",
                        column: x => x.series_id,
                        principalTable: "series",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "episodes",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    program_id = table.Column<int>(type: "INTEGER", nullable: false),
                    ruv_id = table.Column<string>(type: "TEXT", unicode: false, fixedLength: true, maxLength: 6, nullable: false),
                    uri = table.Column<string>(type: "TEXT", unicode: false, maxLength: 256, nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    first_run = table.Column<DateTime>(type: "TEXT", nullable: false),
                    tvdb_id = table.Column<int>(type: "INTEGER", nullable: true),
                    season_number = table.Column<int>(type: "INTEGER", nullable: true),
                    episode_number = table.Column<int>(type: "INTEGER", nullable: true),
                    lookup_count = table.Column<int>(type: "INTEGER", nullable: false),
                    matched = table.Column<DateTime>(type: "TEXT", nullable: true),
                    next_lookup = table.Column<DateTime>(type: "TEXT", nullable: true),
                    download_queue_item_id = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_episodes", x => x.id);
                    table.ForeignKey(
                        name: "fk_episodes_download_queue_download_queue_item_id",
                        column: x => x.download_queue_item_id,
                        principalTable: "download_queue",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_episodes_programs_program_id",
                        column: x => x.program_id,
                        principalTable: "programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_episodes_download_queue_item_id",
                table: "episodes",
                column: "download_queue_item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_episodes_program_id",
                table: "episodes",
                column: "program_id");

            migrationBuilder.CreateIndex(
                name: "ix_episodes_ruv_id",
                table: "episodes",
                column: "ruv_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movies_tmdb_id",
                table: "movies",
                column: "tmdb_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_programs_movie_id",
                table: "programs",
                column: "movie_id");

            migrationBuilder.CreateIndex(
                name: "ix_programs_ruv_id",
                table: "programs",
                column: "ruv_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_programs_series_id",
                table: "programs",
                column: "series_id");

            migrationBuilder.CreateIndex(
                name: "ix_series_tvdb_id",
                table: "series",
                column: "tvdb_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "episodes");

            migrationBuilder.DropTable(
                name: "download_queue");

            migrationBuilder.DropTable(
                name: "programs");

            migrationBuilder.DropTable(
                name: "movies");

            migrationBuilder.DropTable(
                name: "series");
        }
    }
}
