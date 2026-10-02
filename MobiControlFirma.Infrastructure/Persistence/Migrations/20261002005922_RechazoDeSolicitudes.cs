using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobiControlFirma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RechazoDeSolicitudes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SolicitudesFirma_Estado",
                table: "SolicitudesFirma");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRechazo",
                table: "SolicitudesFirma",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoRechazo",
                table: "SolicitudesFirma",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RechazadoPor",
                table: "SolicitudesFirma",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SolicitudesFirma_Estado",
                table: "SolicitudesFirma",
                sql: "[Estado] IN ('PENDIENTE','FIRMADA','RECHAZADA')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SolicitudesFirma_Estado",
                table: "SolicitudesFirma");

            migrationBuilder.DropColumn(
                name: "FechaRechazo",
                table: "SolicitudesFirma");

            migrationBuilder.DropColumn(
                name: "MotivoRechazo",
                table: "SolicitudesFirma");

            migrationBuilder.DropColumn(
                name: "RechazadoPor",
                table: "SolicitudesFirma");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SolicitudesFirma_Estado",
                table: "SolicitudesFirma",
                sql: "[Estado] IN ('PENDIENTE','FIRMADA')");
        }
    }
}
