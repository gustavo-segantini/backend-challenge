using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CnabApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFileUploadLineHashesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileUploadLineHashes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LineHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LineContent = table.Column<string>(type: "text", nullable: false),
                    FileUploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileUploadLineHashes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileUploadLineHashes_FileUploads_FileUploadId",
                        column: x => x.FileUploadId,
                        principalTable: "FileUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileUploadLineHashes_FileUploadId",
                table: "FileUploadLineHashes",
                column: "FileUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_FileUploadLineHashes_LineHash",
                table: "FileUploadLineHashes",
                column: "LineHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileUploadLineHashes");
        }
    }
}
