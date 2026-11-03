using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ShowPaymentTaxa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TaxaPercentual",
                table: "ShowPayments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxaValor",
                table: "ShowPayments",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorTotal",
                table: "ShowPayments",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxaPercentual",
                table: "ShowPayments");

            migrationBuilder.DropColumn(
                name: "TaxaValor",
                table: "ShowPayments");

            migrationBuilder.DropColumn(
                name: "ValorTotal",
                table: "ShowPayments");
        }
    }
}
