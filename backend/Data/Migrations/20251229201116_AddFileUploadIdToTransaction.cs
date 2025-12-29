using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CnabApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFileUploadIdToTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FileUploadId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_FileUploadId",
                table: "Transactions",
                column: "FileUploadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_FileUploadId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "FileUploadId",
                table: "Transactions");
        }
    }
}
