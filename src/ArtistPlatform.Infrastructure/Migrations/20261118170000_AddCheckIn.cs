using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GateAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GateAccesses_TicketEvents_TicketEventId",
                        column: x => x.TicketEventId,
                        principalTable: "TicketEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IssuedTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventTicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CheckedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedInBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CheckedInGateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuedTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssuedTickets_TicketOrders_TicketOrderId",
                        column: x => x.TicketOrderId,
                        principalTable: "TicketOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_GateAccesses_Code", table: "GateAccesses", column: "Code", unique: true);
            migrationBuilder.CreateIndex(name: "IX_GateAccesses_TicketEventId", table: "GateAccesses", column: "TicketEventId");
            migrationBuilder.CreateIndex(name: "IX_IssuedTickets_Code", table: "IssuedTickets", column: "Code", unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_IssuedTickets_TicketOrderId_Sequence",
                table: "IssuedTickets",
                columns: new[] { "TicketOrderId", "Sequence" },
                unique: true);
            migrationBuilder.CreateIndex(name: "IX_IssuedTickets_TicketEventId", table: "IssuedTickets", column: "TicketEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GateAccesses");
            migrationBuilder.DropTable(name: "IssuedTickets");
        }
    }
}
