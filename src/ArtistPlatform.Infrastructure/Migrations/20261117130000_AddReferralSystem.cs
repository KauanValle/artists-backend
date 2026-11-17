using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Coluna de código de indicação no artista
            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "Artists",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Artists_ReferralCode",
                table: "Artists",
                column: "ReferralCode",
                unique: true,
                filter: "\"ReferralCode\" <> ''");

            // 2. Coluna de código de indicação usado no cadastro (AppUser)
            migrationBuilder.AddColumn<string>(
                name: "ReferredByCode",
                table: "AspNetUsers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // 3. Tabela de indicações
            migrationBuilder.CreateTable(
                name: "Referrals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferrerArtistId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferredUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferredUserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referrals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferrerArtistId",
                table: "Referrals",
                column: "ReferrerArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferredUserId",
                table: "Referrals",
                column: "ReferredUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Referrals");

            migrationBuilder.DropIndex(name: "IX_Artists_ReferralCode", table: "Artists");
            migrationBuilder.DropColumn(name: "ReferralCode", table: "Artists");

            migrationBuilder.DropColumn(name: "ReferredByCode", table: "AspNetUsers");
        }
    }
}
