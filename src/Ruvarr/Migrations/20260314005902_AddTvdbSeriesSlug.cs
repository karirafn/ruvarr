using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ruvarr.Migrations
{
    /// <inheritdoc />
    public partial class AddTvdbSeriesSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "series",
                type: "TEXT",
                unicode: false,
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "slug",
                table: "series");
        }
    }
}
