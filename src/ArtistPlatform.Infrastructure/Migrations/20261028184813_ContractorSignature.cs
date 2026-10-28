using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ContractorSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContractorAccessToken",
                table: "SignatureContracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractorContentHash",
                table: "SignatureContracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractorIp",
                table: "SignatureContracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractorSignatureData",
                table: "SignatureContracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContractorSignedAtUtc",
                table: "SignatureContracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractorSignedDocument",
                table: "SignatureContracts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContractorSignedName",
                table: "SignatureContracts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContractorUserAgent",
                table: "SignatureContracts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignatureContracts_ContractorAccessToken",
                table: "SignatureContracts",
                column: "ContractorAccessToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SignatureContracts_ContractorAccessToken",
                table: "SignatureContracts");

            migrationBuilder.DropColumn(
                name: "ContractorAccessToken",
                table: "SignatureContracts");

            migrationBuilder.DropColumn(
                name: "ContractorContentHash",
                table: "SignatureContracts");

            migrationBuilder.DropColumn(
                name: "ContractorIp",
                table: "SignatureContracts");

            migrationBuilder.DropColumn(
                name: "ContractorSignatureData",
                table: "SignatureContracts");

            migrationBuilder.DropColumn(
                name: "ContractorSignedAtUtc",
                table: "SignatureContracts");

            migrationBuilder.DropColumn(
                name: "ContractorSignedDocument",
                table: "SignatureContracts");

            migrationBuilder.DropColumn(
                name: "ContractorSignedName",
                table: "SignatureContracts");

            migrationBuilder.DropColumn(
                name: "ContractorUserAgent",
                table: "SignatureContracts");
        }
    }
}
