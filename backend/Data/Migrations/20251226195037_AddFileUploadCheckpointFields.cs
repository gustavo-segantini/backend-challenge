using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CnabApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFileUploadCheckpointFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLineCount",
                table: "FileUploads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCheckpointAt",
                table: "FileUploads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastCheckpointLine",
                table: "FileUploads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SkippedLineCount",
                table: "FileUploads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalLineCount",
                table: "FileUploads",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedLineCount",
                table: "FileUploads");

            migrationBuilder.DropColumn(
                name: "LastCheckpointAt",
                table: "FileUploads");

            migrationBuilder.DropColumn(
                name: "LastCheckpointLine",
                table: "FileUploads");

            migrationBuilder.DropColumn(
                name: "SkippedLineCount",
                table: "FileUploads");

            migrationBuilder.DropColumn(
                name: "TotalLineCount",
                table: "FileUploads");
        }
    }
}
