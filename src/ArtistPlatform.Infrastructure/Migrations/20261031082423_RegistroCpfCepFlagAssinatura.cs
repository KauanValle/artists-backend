using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RegistroCpfCepFlagAssinatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cep",
                table: "Contractors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cpf",
                table: "Contractors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cep",
                table: "Artists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cpf",
                table: "Artists",
                type: "text",
                nullable: true);

            // Feature flag: assinatura DESLIGADA por padrão — contas gratuitas têm acesso livre.
            // O admin liga/desliga alterando o valor desta linha ("true"/"false").
            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Id", "Key", "Value", "CreatedAt" },
                values: new object[]
                {
                    new Guid("7f3c2a5e-9b41-4d6f-8a27-c5e1d0b9a001"),
                    "assinatura:obrigatoria",
                    "false",
                    new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc)
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AppSettings",
                keyColumn: "Key",
                keyValue: "assinatura:obrigatoria");

            migrationBuilder.DropColumn(
                name: "Cep",
                table: "Contractors");

            migrationBuilder.DropColumn(
                name: "Cpf",
                table: "Contractors");

            migrationBuilder.DropColumn(
                name: "Cep",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "Cpf",
                table: "Artists");
        }
    }
}
