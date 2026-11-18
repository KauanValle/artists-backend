using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventTicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BuyerPhone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    BuyerEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BuyerCpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketOrders_EventTickets_EventTicketId",
                        column: x => x.EventTicketId,
                        principalTable: "EventTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketOrders_EventTicketId",
                table: "TicketOrders",
                column: "EventTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketOrders_TicketEventId",
                table: "TicketOrders",
                column: "TicketEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketOrders");
        }
    }
}
