using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistPlatform.Infrastructure.Migrations;

public partial class AddCampaigns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Campaigns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                Rules = table.Column<string>(type: "text", nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false),
                StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                GoalDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                GoalTarget = table.Column<long>(type: "bigint", nullable: true),
                GoalCurrent = table.Column<long>(type: "bigint", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Campaigns", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Campaigns_Slug",
            table: "Campaigns",
            column: "Slug",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Campaigns");
    }
}
