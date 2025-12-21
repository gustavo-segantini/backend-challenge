using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System.Diagnostics.CodeAnalysis;

#nullable disable

namespace CnabApi.Data.Migrations
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CpfCnpj = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Card = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(15,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BankCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Account = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    NatureCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    CpfCnpj = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Card = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TransactionTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stores_CpfCnpj_Card",
                table: "Stores",
                columns: new[] { "CpfCnpj", "Card" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionDate_CpfCnpj",
                table: "Transactions",
                columns: new[] { "TransactionDate", "CpfCnpj" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropTable(
                name: "Transactions");
        }
    }
}
