using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ruvarr.Migrations
{
    /// <inheritdoc />
    public partial class MoveDownloadFkToQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "episode_id",
                table: "download_queue",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE download_queue SET episode_id = (SELECT e.id FROM episodes e WHERE e.download_queue_item_id = download_queue.id)");

            migrationBuilder.DropForeignKey(
                name: "fk_episodes_download_queue_download_queue_item_id",
                table: "episodes");

            migrationBuilder.DropIndex(
                name: "ix_episodes_download_queue_item_id",
                table: "episodes");

            migrationBuilder.DropColumn(
                name: "download_queue_item_id",
                table: "episodes");

            migrationBuilder.CreateIndex(
                name: "ix_download_queue_episode_id",
                table: "download_queue",
                column: "episode_id");

            migrationBuilder.AddForeignKey(
                name: "fk_download_queue_episodes_episode_id",
                table: "download_queue",
                column: "episode_id",
                principalTable: "episodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_download_queue_episodes_episode_id",
                table: "download_queue");

            migrationBuilder.DropIndex(
                name: "ix_download_queue_episode_id",
                table: "download_queue");

            migrationBuilder.DropColumn(
                name: "episode_id",
                table: "download_queue");

            migrationBuilder.AddColumn<int>(
                name: "download_queue_item_id",
                table: "episodes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE episodes SET download_queue_item_id = ("
                + "SELECT dq.id FROM download_queue dq "
                + "WHERE dq.episode_id = episodes.id "
                + "ORDER BY dq.created DESC LIMIT 1)");

            migrationBuilder.CreateIndex(
                name: "ix_episodes_download_queue_item_id",
                table: "episodes",
                column: "download_queue_item_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_episodes_download_queue_download_queue_item_id",
                table: "episodes",
                column: "download_queue_item_id",
                principalTable: "download_queue",
                principalColumn: "id");
        }
    }
}
