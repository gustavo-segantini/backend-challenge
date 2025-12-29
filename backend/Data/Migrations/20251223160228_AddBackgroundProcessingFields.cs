using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CnabApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundProcessingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingCompletedAt",
                table: "FileUploads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartedAt",
                table: "FileUploads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "FileUploads",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessingCompletedAt",
                table: "FileUploads");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAt",
                table: "FileUploads");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "FileUploads");
        }
    }
}
