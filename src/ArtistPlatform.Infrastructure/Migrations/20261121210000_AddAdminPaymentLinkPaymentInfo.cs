using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminPaymentLinkPaymentInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "PaidVia", table: "AdminPaymentLinks",
                type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "PaymentMethod", table: "AdminPaymentLinks",
                type: "character varying(40)", maxLength: 40, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReceiptUrl", table: "AdminPaymentLinks",
                type: "character varying(1000)", maxLength: 1000, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PaidVia", table: "AdminPaymentLinks");
            migrationBuilder.DropColumn(name: "PaymentMethod", table: "AdminPaymentLinks");
            migrationBuilder.DropColumn(name: "ReceiptUrl", table: "AdminPaymentLinks");
        }
    }
}
