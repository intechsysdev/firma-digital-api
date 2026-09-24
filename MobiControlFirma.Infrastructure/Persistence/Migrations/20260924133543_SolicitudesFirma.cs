using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobiControlFirma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SolicitudesFirma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolicitudesFirma",
                columns: table => new
                {
                    SolicitudId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    SolicitudUid = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    IdSolicitudOrigen = table.Column<string>(type: "varchar(100)", nullable: false),
                    DatosOrigen = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Estado = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "PENDIENTE"),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FechaVencimiento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFirma = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EntregaId = table.Column<int>(type: "int", nullable: true),
                    EstadoCallback = table.Column<string>(type: "varchar(20)", nullable: true),
                    IntentosCallback = table.Column<int>(type: "int", nullable: false),
                    CodigoHttpCallback = table.Column<int>(type: "int", nullable: true),
                    UltimoErrorCallback = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProximoIntentoCallback = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaCallback = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesFirma", x => x.SolicitudId);
                    table.CheckConstraint("CK_SolicitudesFirma_DatosJson", "ISJSON([DatosOrigen]) = 1");
                    table.CheckConstraint("CK_SolicitudesFirma_Estado", "[Estado] IN ('PENDIENTE','FIRMADA')");
                    table.ForeignKey(
                        name: "FK_SolicitudesFirma_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "EmpresaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolicitudesFirma_EntregasDispositivo_EntregaId",
                        column: x => x.EntregaId,
                        principalTable: "EntregasDispositivo",
                        principalColumn: "EntregaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesFirma_CallbacksPendientes",
                table: "SolicitudesFirma",
                columns: new[] { "EstadoCallback", "ProximoIntentoCallback" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesFirma_EmpresaId",
                table: "SolicitudesFirma",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesFirma_EntregaId",
                table: "SolicitudesFirma",
                column: "EntregaId",
                unique: true,
                filter: "[EntregaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesFirma_SolicitudUid",
                table: "SolicitudesFirma",
                column: "SolicitudUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_SolicitudesFirma_Origen",
                table: "SolicitudesFirma",
                columns: new[] { "EmpresaId", "IdSolicitudOrigen" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SolicitudesFirma");
        }
    }
}
