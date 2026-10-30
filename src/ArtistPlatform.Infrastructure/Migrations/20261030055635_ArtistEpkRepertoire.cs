using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ArtistEpkRepertoire : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Songs",
                table: "ArtistEpks",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SoundCloudUrl",
                table: "ArtistEpks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpotifyPlaylistUrl",
                table: "ArtistEpks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpotifyUrl",
                table: "ArtistEpks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YouTubeUrl",
                table: "ArtistEpks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Songs",
                table: "ArtistEpks");

            migrationBuilder.DropColumn(
                name: "SoundCloudUrl",
                table: "ArtistEpks");

            migrationBuilder.DropColumn(
                name: "SpotifyPlaylistUrl",
                table: "ArtistEpks");

            migrationBuilder.DropColumn(
                name: "SpotifyUrl",
                table: "ArtistEpks");

            migrationBuilder.DropColumn(
                name: "YouTubeUrl",
                table: "ArtistEpks");
        }
    }
}
