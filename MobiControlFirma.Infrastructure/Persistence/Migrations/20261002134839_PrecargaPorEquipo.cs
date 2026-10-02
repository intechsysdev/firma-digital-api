using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobiControlFirma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrecargaPorEquipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaActualizacion",
                table: "SolicitudesFirma",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "Imei",
                table: "SolicitudesFirma",
                type: "varchar(50)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Serial",
                table: "SolicitudesFirma",
                type: "varchar(100)",
                nullable: true);

            // Las solicitudes que ya existían: el equipo y la fecha del último cambio salen de lo
            // que ya está guardado. El IMEI y el serial van sin separadores, como los busca el API.
            migrationBuilder.Sql("""
                UPDATE SolicitudesFirma SET
                    Imei = NULLIF(UPPER(REPLACE(REPLACE(REPLACE(JSON_VALUE(DatosOrigen, '$.imei'), ' ', ''), '-', ''), '.', '')), ''),
                    Serial = NULLIF(UPPER(REPLACE(REPLACE(REPLACE(JSON_VALUE(DatosOrigen, '$.serial'), ' ', ''), '-', ''), '.', '')), ''),
                    FechaActualizacion = COALESCE(FechaFirma, FechaRechazo, FechaCreacion);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesFirma_Cambios",
                table: "SolicitudesFirma",
                columns: new[] { "EmpresaId", "FechaActualizacion" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesFirma_Imei",
                table: "SolicitudesFirma",
                columns: new[] { "EmpresaId", "Imei" },
                filter: "[Imei] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesFirma_Serial",
                table: "SolicitudesFirma",
                columns: new[] { "EmpresaId", "Serial" },
                filter: "[Serial] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SolicitudesFirma_Cambios",
                table: "SolicitudesFirma");

            migrationBuilder.DropIndex(
                name: "IX_SolicitudesFirma_Imei",
                table: "SolicitudesFirma");

            migrationBuilder.DropIndex(
                name: "IX_SolicitudesFirma_Serial",
                table: "SolicitudesFirma");

            migrationBuilder.DropColumn(
                name: "FechaActualizacion",
                table: "SolicitudesFirma");

            migrationBuilder.DropColumn(
                name: "Imei",
                table: "SolicitudesFirma");

            migrationBuilder.DropColumn(
                name: "Serial",
                table: "SolicitudesFirma");
        }
    }
}
