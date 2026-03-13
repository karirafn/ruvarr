using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ruvarr.Migrations
{
    /// <inheritdoc />
    public partial class AddHasMissingEpisodesToRuvProgram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_missing_episodes",
                table: "programs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_missing_episodes",
                table: "programs");
        }
    }
}
