using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EventCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArtistCompletedAt",
                table: "Events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionToken",
                table: "Events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContractorCompletedAt",
                table: "Events",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArtistCompletedAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "CompletionToken",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ContractorCompletedAt",
                table: "Events");
        }
    }
}
