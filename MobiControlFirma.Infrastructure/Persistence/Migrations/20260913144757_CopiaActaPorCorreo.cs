using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobiControlFirma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CopiaActaPorCorreo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorreoAsociado",
                table: "EntregasDispositivo",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorreosCopia",
                table: "Empresas",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InfobipApiKey",
                table: "Empresas",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InfobipBaseUrl",
                table: "Empresas",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InfobipNombreRemitente",
                table: "Empresas",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InfobipRemitente",
                table: "Empresas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EnviosCorreo",
                columns: table => new
                {
                    EnvioId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    EntregaId = table.Column<int>(type: "int", nullable: false),
                    Destinatarios = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Asunto = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Estado = table.Column<string>(type: "varchar(20)", nullable: false),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    UltimoError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProximoIntento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FechaEnvio = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnviosCorreo", x => x.EnvioId);
                    table.ForeignKey(
                        name: "FK_EnviosCorreo_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "EmpresaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnviosCorreo_EntregasDispositivo_EntregaId",
                        column: x => x.EntregaId,
                        principalTable: "EntregasDispositivo",
                        principalColumn: "EntregaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnviosCorreo_EmpresaId",
                table: "EnviosCorreo",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_EnviosCorreo_EntregaId",
                table: "EnviosCorreo",
                column: "EntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_EnviosCorreo_Pendientes",
                table: "EnviosCorreo",
                columns: new[] { "Estado", "ProximoIntento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnviosCorreo");

            migrationBuilder.DropColumn(
                name: "CorreoAsociado",
                table: "EntregasDispositivo");

            migrationBuilder.DropColumn(
                name: "CorreosCopia",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "InfobipApiKey",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "InfobipBaseUrl",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "InfobipNombreRemitente",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "InfobipRemitente",
                table: "Empresas");
        }
    }
}
