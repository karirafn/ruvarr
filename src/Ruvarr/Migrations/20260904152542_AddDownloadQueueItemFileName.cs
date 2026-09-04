using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ruvarr.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadQueueItemFileName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "file_name",
                table: "download_queue",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "file_name",
                table: "download_queue");
        }
    }
}
