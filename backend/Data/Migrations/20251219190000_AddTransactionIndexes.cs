using Microsoft.EntityFrameworkCore.Migrations;
using System.Diagnostics.CodeAnalysis;

#nullable disable

namespace CnabApi.Data.Migrations
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class AddTransactionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Cpf",
                table: "Transactions",
                column: "Cpf");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_NatureCode",
                table: "Transactions",
                column: "NatureCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_Cpf",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_NatureCode",
                table: "Transactions");
        }
    }
}