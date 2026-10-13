using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobiControlFirma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SolicitudesDesdeConsola : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CreadaEnConsola",
                table: "SolicitudesFirma",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CreadaPor",
                table: "SolicitudesFirma",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                table: "SolicitudesFirma",
                type: "varchar(100)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesFirma_DeviceId",
                table: "SolicitudesFirma",
                columns: new[] { "EmpresaId", "DeviceId" },
                filter: "[DeviceId] IS NOT NULL");

            // Las solicitudes que ya existían tienen el equipo solo dentro del JSON.
            migrationBuilder.Sql(
                "UPDATE SolicitudesFirma SET DeviceId = LEFT(JSON_VALUE(DatosOrigen, '$.deviceId'), 100) " +
                "WHERE DeviceId IS NULL AND JSON_VALUE(DatosOrigen, '$.deviceId') IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SolicitudesFirma_DeviceId",
                table: "SolicitudesFirma");

            migrationBuilder.DropColumn(
                name: "CreadaEnConsola",
                table: "SolicitudesFirma");

            migrationBuilder.DropColumn(
                name: "CreadaPor",
                table: "SolicitudesFirma");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "SolicitudesFirma");
        }
    }
}
