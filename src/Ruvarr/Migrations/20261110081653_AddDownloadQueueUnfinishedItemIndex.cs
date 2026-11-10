using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ruvarr.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadQueueUnfinishedItemIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_download_queue_episode_id_unfinished",
                table: "download_queue",
                column: "episode_id",
                unique: true,
                filter: "\"status\" <> 'Complete'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_download_queue_episode_id_unfinished",
                table: "download_queue");
        }
    }
}
