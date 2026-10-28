using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SignatureContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SignatureContracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtistId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContractText = table.Column<string>(type: "text", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ArtistName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ArtistDocument = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ArtistAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ArtistContact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContractorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContractorDocument = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ContractorRepresentative = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContractorAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContractorContact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PaymentTerms = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ArtistSignatureData = table.Column<string>(type: "text", nullable: true),
                    ArtistSignedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ArtistSignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArtistIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ArtistUserAgent = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatureContracts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignatureContracts_ArtistId",
                table: "SignatureContracts",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureContracts_BookingRequestId",
                table: "SignatureContracts",
                column: "BookingRequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignatureContracts");
        }
    }
}
