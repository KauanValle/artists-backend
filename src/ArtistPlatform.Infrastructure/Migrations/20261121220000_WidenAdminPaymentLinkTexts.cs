using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WidenAdminPaymentLinkTexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(name: "Description", table: "AdminPaymentLinks",
                type: "character varying(1000)", maxLength: 1000, nullable: false,
                oldClrType: typeof(string), oldType: "character varying(200)", oldMaxLength: 200);
            migrationBuilder.AlterColumn<string>(name: "Note", table: "AdminPaymentLinks",
                type: "character varying(2000)", maxLength: 2000, nullable: true,
                oldClrType: typeof(string), oldType: "character varying(500)", oldMaxLength: 500, oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(name: "Description", table: "AdminPaymentLinks",
                type: "character varying(200)", maxLength: 200, nullable: false,
                oldClrType: typeof(string), oldType: "character varying(1000)", oldMaxLength: 1000);
            migrationBuilder.AlterColumn<string>(name: "Note", table: "AdminPaymentLinks",
                type: "character varying(500)", maxLength: 500, nullable: true,
                oldClrType: typeof(string), oldType: "character varying(2000)", oldMaxLength: 2000, oldNullable: true);
        }
    }
}
