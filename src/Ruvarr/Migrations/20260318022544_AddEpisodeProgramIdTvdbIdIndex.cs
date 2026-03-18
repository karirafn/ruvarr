using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ruvarr.Migrations
{
    /// <inheritdoc />
    public partial class AddEpisodeProgramIdTvdbIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_episodes_program_id",
                table: "episodes");

            migrationBuilder.CreateIndex(
                name: "ix_episodes_program_id_tvdb_id",
                table: "episodes",
                columns: new[] { "program_id", "tvdb_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_episodes_program_id_tvdb_id",
                table: "episodes");

            migrationBuilder.CreateIndex(
                name: "ix_episodes_program_id",
                table: "episodes",
                column: "program_id");
        }
    }
}
