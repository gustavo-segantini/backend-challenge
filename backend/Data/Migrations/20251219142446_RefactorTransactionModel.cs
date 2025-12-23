using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System.Diagnostics.CodeAnalysis;

#nullable disable

namespace CnabApi.Data.Migrations
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class RefactorTransactionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_TransactionDate_CpfCnpj",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Account",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CpfCnpj",
                table: "Transactions");

            migrationBuilder.AlterColumn<string>(
                name: "NatureCode",
                table: "Transactions",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5);

            migrationBuilder.AlterColumn<string>(
                name: "Card",
                table: "Transactions",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(25)",
                oldMaxLength: 25);

            migrationBuilder.AddColumn<string>(
                name: "Cpf",
                table: "Transactions",
                type: "character varying(11)",
                maxLength: 11,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionDate_Cpf",
                table: "Transactions",
                columns: ["TransactionDate", "Cpf"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_TransactionDate_Cpf",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Cpf",
                table: "Transactions");

            migrationBuilder.AlterColumn<string>(
                name: "NatureCode",
                table: "Transactions",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12);

            migrationBuilder.AlterColumn<string>(
                name: "Card",
                table: "Transactions",
                type: "character varying(25)",
                maxLength: 25,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12);

            migrationBuilder.AddColumn<string>(
                name: "Account",
                table: "Transactions",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CpfCnpj",
                table: "Transactions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Balance = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    Card = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    CpfCnpj = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionDate_CpfCnpj",
                table: "Transactions",
                columns: ["TransactionDate", "CpfCnpj"]);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_CpfCnpj_Card",
                table: "Stores",
                columns: ["CpfCnpj", "Card"],
                unique: true);
        }
    }
}
