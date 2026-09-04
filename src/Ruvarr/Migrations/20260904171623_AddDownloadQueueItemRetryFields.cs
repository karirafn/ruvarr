using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ruvarr.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadQueueItemRetryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "failure_reason",
                table: "download_queue",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_retry_at",
                table: "download_queue",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retry_count",
                table: "download_queue",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failure_reason",
                table: "download_queue");

            migrationBuilder.DropColumn(
                name: "next_retry_at",
                table: "download_queue");

            migrationBuilder.DropColumn(
                name: "retry_count",
                table: "download_queue");
        }
    }
}
