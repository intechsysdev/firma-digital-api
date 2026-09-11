using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobiControlFirma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Multiempresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_IntegracionesConfig_Proveedor_Entorno",
                table: "IntegracionesConfiguracion");

            migrationBuilder.DropIndex(
                name: "IX_EstadosDispositivo_Nombre",
                table: "EstadosDispositivo");

            migrationBuilder.DropIndex(
                name: "UQ_EntregasDispositivo_Idempotencia",
                table: "EntregasDispositivo");

            migrationBuilder.DropIndex(
                name: "IX_Empleados_Cedula",
                table: "Empleados");

            migrationBuilder.DropIndex(
                name: "IX_Distritos_Nombre",
                table: "Distritos");

            migrationBuilder.DropIndex(
                name: "IX_Dispositivos_IMEI",
                table: "Dispositivos");

            migrationBuilder.DropIndex(
                name: "IX_Dispositivos_MobiControlDeviceId",
                table: "Dispositivos");

            migrationBuilder.DropIndex(
                name: "IX_Canales_Nombre",
                table: "Canales");

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "IntegracionesSincronizaciones",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "IntegracionesConfiguracion",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "Firmas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "EstadosDispositivo",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "EntregasDispositivo",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "Empleados",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "DocumentosPDF",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "Distritos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "Dispositivos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "Canales",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    EmpresaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Nit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CiudadFirma = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "Cali"),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ApiKeyHash = table.Column<byte[]>(type: "varbinary(32)", nullable: false),
                    ApiKeyPrefijo = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    ApiKeyRotadaEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MobiControlBaseUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MobiControlClientId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MobiControlClientSecret = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MobiControlUsuario = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    MobiControlPassword = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MobiControlAtributoFirma = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MobiControlAtributoFecha = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MobiControlTimeoutSegundos = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.EmpresaId);
                });

            // Todo lo que ya existe tiene dueño desde el primer día del multiempresa. Las
            // columnas se agregaron arriba con valor 0, que no apunta a ninguna empresa: sin
            // este traspaso, las llaves foráneas de más abajo no se podrían crear.
            //
            // La llave de esta empresa es la que ya llevan instalados los equipos en campo, de
            // modo que siguen registrando actas sin tener que volver a desplegar el formulario.
            // Se guarda su resumen SHA-256, igual que para cualquier otra empresa.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [Empresas])
                BEGIN
                    INSERT INTO [Empresas]
                        ([Nombre], [Nit], [CiudadFirma], [Activo], [ApiKeyHash], [ApiKeyPrefijo],
                         [MobiControlAtributoFirma], [MobiControlAtributoFecha], [MobiControlTimeoutSegundos])
                    VALUES
                        (N'Intechsys', NULL, N'Cali', 1,
                         0x291C7D5C0AE3568D90D9D8A2DF69BFDA8B661DB1EA6381CEA3D92130D2971FBA,
                         N'dev-disp', N'Firma de entrega', N'Fecha de entrega', 20);
                END;

                DECLARE @empresa INT = (SELECT MIN([EmpresaId]) FROM [Empresas]);

                UPDATE [Distritos]                      SET [EmpresaId] = @empresa WHERE [EmpresaId] = 0;
                UPDATE [Canales]                        SET [EmpresaId] = @empresa WHERE [EmpresaId] = 0;
                UPDATE [EstadosDispositivo]             SET [EmpresaId] = @empresa WHERE [EmpresaId] = 0;
                UPDATE [Empleados]                      SET [EmpresaId] = @empresa WHERE [EmpresaId] = 0;
                UPDATE [Dispositivos]                   SET [EmpresaId] = @empresa WHERE [EmpresaId] = 0;
                UPDATE [EntregasDispositivo]            SET [EmpresaId] = @empresa WHERE [EmpresaId] = 0;
                UPDATE [Firmas]                         SET [EmpresaId] = @empresa WHERE [EmpresaId] = 0;
                UPDATE [DocumentosPDF]                  SET [EmpresaId] = @empresa WHERE [EmpresaId] = 0;
                UPDATE [IntegracionesSincronizaciones]  SET [EmpresaId] = @empresa WHERE [EmpresaId] = 0;
                UPDATE [IntegracionesConfiguracion]     SET [EmpresaId] = @empresa WHERE [EmpresaId] = 0;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_IntegracionesSincronizaciones_EmpresaId",
                table: "IntegracionesSincronizaciones",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegracionesConfiguracion_EmpresaId",
                table: "IntegracionesConfiguracion",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "UQ_IntegracionesConfig_Proveedor_Entorno",
                table: "IntegracionesConfiguracion",
                columns: new[] { "EmpresaId", "Proveedor", "Entorno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Firmas_EmpresaId",
                table: "Firmas",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_EstadosDispositivo_EmpresaId",
                table: "EstadosDispositivo",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_EstadosDispositivo_EmpresaId_Nombre",
                table: "EstadosDispositivo",
                columns: new[] { "EmpresaId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntregasDispositivo_EmpresaId",
                table: "EntregasDispositivo",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "UQ_EntregasDispositivo_Idempotencia",
                table: "EntregasDispositivo",
                columns: new[] { "EmpresaId", "ClaveIdempotencia" },
                unique: true,
                filter: "[ClaveIdempotencia] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Empleados_EmpresaId",
                table: "Empleados",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Empleados_EmpresaId_Cedula",
                table: "Empleados",
                columns: new[] { "EmpresaId", "Cedula" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosPDF_EmpresaId",
                table: "DocumentosPDF",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Distritos_EmpresaId",
                table: "Distritos",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Distritos_EmpresaId_Nombre",
                table: "Distritos",
                columns: new[] { "EmpresaId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_EmpresaId",
                table: "Dispositivos",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_EmpresaId_IMEI",
                table: "Dispositivos",
                columns: new[] { "EmpresaId", "IMEI" },
                unique: true,
                filter: "[IMEI] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_EmpresaId_MobiControlDeviceId",
                table: "Dispositivos",
                columns: new[] { "EmpresaId", "MobiControlDeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Canales_EmpresaId",
                table: "Canales",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Canales_EmpresaId_Nombre",
                table: "Canales",
                columns: new[] { "EmpresaId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_Nombre",
                table: "Empresas",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Empresas_ApiKeyHash",
                table: "Empresas",
                column: "ApiKeyHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Canales_Empresas_EmpresaId",
                table: "Canales",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "EmpresaId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Dispositivos_Empresas_EmpresaId",
                table: "Dispositivos",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "EmpresaId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Distritos_Empresas_EmpresaId",
                table: "Distritos",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "EmpresaId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentosPDF_Empresas_EmpresaId",
                table: "DocumentosPDF",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "EmpresaId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Empleados_Empresas_EmpresaId",
                table: "Empleados",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "EmpresaId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EntregasDispositivo_Empresas_EmpresaId",
                table: "EntregasDispositivo",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "EmpresaId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EstadosDispositivo_Empresas_EmpresaId",
                table: "EstadosDispositivo",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "EmpresaId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Firmas_Empresas_EmpresaId",
                table: "Firmas",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "EmpresaId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IntegracionesConfiguracion_Empresas_EmpresaId",
                table: "IntegracionesConfiguracion",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "EmpresaId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IntegracionesSincronizaciones_Empresas_EmpresaId",
                table: "IntegracionesSincronizaciones",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "EmpresaId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Canales_Empresas_EmpresaId",
                table: "Canales");

            migrationBuilder.DropForeignKey(
                name: "FK_Dispositivos_Empresas_EmpresaId",
                table: "Dispositivos");

            migrationBuilder.DropForeignKey(
                name: "FK_Distritos_Empresas_EmpresaId",
                table: "Distritos");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentosPDF_Empresas_EmpresaId",
                table: "DocumentosPDF");

            migrationBuilder.DropForeignKey(
                name: "FK_Empleados_Empresas_EmpresaId",
                table: "Empleados");

            migrationBuilder.DropForeignKey(
                name: "FK_EntregasDispositivo_Empresas_EmpresaId",
                table: "EntregasDispositivo");

            migrationBuilder.DropForeignKey(
                name: "FK_EstadosDispositivo_Empresas_EmpresaId",
                table: "EstadosDispositivo");

            migrationBuilder.DropForeignKey(
                name: "FK_Firmas_Empresas_EmpresaId",
                table: "Firmas");

            migrationBuilder.DropForeignKey(
                name: "FK_IntegracionesConfiguracion_Empresas_EmpresaId",
                table: "IntegracionesConfiguracion");

            migrationBuilder.DropForeignKey(
                name: "FK_IntegracionesSincronizaciones_Empresas_EmpresaId",
                table: "IntegracionesSincronizaciones");

            migrationBuilder.DropTable(
                name: "Empresas");

            migrationBuilder.DropIndex(
                name: "IX_IntegracionesSincronizaciones_EmpresaId",
                table: "IntegracionesSincronizaciones");

            migrationBuilder.DropIndex(
                name: "IX_IntegracionesConfiguracion_EmpresaId",
                table: "IntegracionesConfiguracion");

            migrationBuilder.DropIndex(
                name: "UQ_IntegracionesConfig_Proveedor_Entorno",
                table: "IntegracionesConfiguracion");

            migrationBuilder.DropIndex(
                name: "IX_Firmas_EmpresaId",
                table: "Firmas");

            migrationBuilder.DropIndex(
                name: "IX_EstadosDispositivo_EmpresaId",
                table: "EstadosDispositivo");

            migrationBuilder.DropIndex(
                name: "IX_EstadosDispositivo_EmpresaId_Nombre",
                table: "EstadosDispositivo");

            migrationBuilder.DropIndex(
                name: "IX_EntregasDispositivo_EmpresaId",
                table: "EntregasDispositivo");

            migrationBuilder.DropIndex(
                name: "UQ_EntregasDispositivo_Idempotencia",
                table: "EntregasDispositivo");

            migrationBuilder.DropIndex(
                name: "IX_Empleados_EmpresaId",
                table: "Empleados");

            migrationBuilder.DropIndex(
                name: "IX_Empleados_EmpresaId_Cedula",
                table: "Empleados");

            migrationBuilder.DropIndex(
                name: "IX_DocumentosPDF_EmpresaId",
                table: "DocumentosPDF");

            migrationBuilder.DropIndex(
                name: "IX_Distritos_EmpresaId",
                table: "Distritos");

            migrationBuilder.DropIndex(
                name: "IX_Distritos_EmpresaId_Nombre",
                table: "Distritos");

            migrationBuilder.DropIndex(
                name: "IX_Dispositivos_EmpresaId",
                table: "Dispositivos");

            migrationBuilder.DropIndex(
                name: "IX_Dispositivos_EmpresaId_IMEI",
                table: "Dispositivos");

            migrationBuilder.DropIndex(
                name: "IX_Dispositivos_EmpresaId_MobiControlDeviceId",
                table: "Dispositivos");

            migrationBuilder.DropIndex(
                name: "IX_Canales_EmpresaId",
                table: "Canales");

            migrationBuilder.DropIndex(
                name: "IX_Canales_EmpresaId_Nombre",
                table: "Canales");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "IntegracionesSincronizaciones");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "IntegracionesConfiguracion");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Firmas");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "EstadosDispositivo");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "EntregasDispositivo");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Empleados");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "DocumentosPDF");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Distritos");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Canales");

            migrationBuilder.DropColumn(
                name: "Activo",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "UQ_IntegracionesConfig_Proveedor_Entorno",
                table: "IntegracionesConfiguracion",
                columns: new[] { "Proveedor", "Entorno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EstadosDispositivo_Nombre",
                table: "EstadosDispositivo",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_EntregasDispositivo_Idempotencia",
                table: "EntregasDispositivo",
                column: "ClaveIdempotencia",
                unique: true,
                filter: "[ClaveIdempotencia] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Empleados_Cedula",
                table: "Empleados",
                column: "Cedula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Distritos_Nombre",
                table: "Distritos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_IMEI",
                table: "Dispositivos",
                column: "IMEI",
                unique: true,
                filter: "[IMEI] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_MobiControlDeviceId",
                table: "Dispositivos",
                column: "MobiControlDeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Canales_Nombre",
                table: "Canales",
                column: "Nombre",
                unique: true);
        }
    }
}
