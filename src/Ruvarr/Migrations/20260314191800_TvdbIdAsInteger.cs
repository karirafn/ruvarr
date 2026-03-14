using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ruvarr.Migrations
{
    /// <inheritdoc />
    public partial class TvdbIdAsInteger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "tvdb_id",
                table: "series",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldUnicode: false,
                oldMaxLength: 16);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "tvdb_id",
                table: "series",
                type: "TEXT",
                unicode: false,
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }
    }
}
