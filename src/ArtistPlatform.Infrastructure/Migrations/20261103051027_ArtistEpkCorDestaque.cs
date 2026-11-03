using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ArtistEpkCorDestaque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorDestaque",
                table: "ArtistEpks",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorDestaque",
                table: "ArtistEpks");
        }
    }
}
