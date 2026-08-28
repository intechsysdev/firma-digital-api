using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobiControlFirma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Canales",
                columns: table => new
                {
                    CanalId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Canales", x => x.CanalId);
                });

            migrationBuilder.CreateTable(
                name: "Distritos",
                columns: table => new
                {
                    DistritoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Distritos", x => x.DistritoId);
                });

            migrationBuilder.CreateTable(
                name: "EstadosDispositivo",
                columns: table => new
                {
                    EstadoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosDispositivo", x => x.EstadoId);
                });

            migrationBuilder.CreateTable(
                name: "IntegracionesConfiguracion",
                columns: table => new
                {
                    ConfiguracionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Proveedor = table.Column<string>(type: "varchar(30)", nullable: false),
                    Entorno = table.Column<string>(type: "varchar(50)", nullable: false, defaultValue: "PRODUCCION"),
                    TipoAutenticacion = table.Column<string>(type: "varchar(30)", nullable: false),
                    UrlBase = table.Column<string>(type: "varchar(300)", nullable: false),
                    ClientIdCifrado = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ClientSecretCifrado = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    UsuarioCifrado = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PasswordCifrado = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ApiKeyCifrada = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ParametrosAdicionales = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegracionesConfiguracion", x => x.ConfiguracionId);
                    table.CheckConstraint("CK_IntegracionesConfiguracion_ParametrosJson", "[ParametrosAdicionales] IS NULL OR ISJSON([ParametrosAdicionales]) = 1");
                    table.CheckConstraint("CK_IntegracionesConfiguracion_Proveedor", "[Proveedor] IN ('MOBICONTROL','INFOBIP','GUPSHUP')");
                    table.CheckConstraint("CK_IntegracionesConfiguracion_TipoAutenticacion", "[TipoAutenticacion] IN ('OAUTH_PASSWORD','API_KEY','BASIC')");
                });

            migrationBuilder.CreateTable(
                name: "Empleados",
                columns: table => new
                {
                    EmpleadoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Cedula = table.Column<string>(type: "varchar(20)", nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CanalId = table.Column<int>(type: "int", nullable: true),
                    DistritoId = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empleados", x => x.EmpleadoId);
                    table.ForeignKey(
                        name: "FK_Empleados_Canales_CanalId",
                        column: x => x.CanalId,
                        principalTable: "Canales",
                        principalColumn: "CanalId");
                    table.ForeignKey(
                        name: "FK_Empleados_Distritos_DistritoId",
                        column: x => x.DistritoId,
                        principalTable: "Distritos",
                        principalColumn: "DistritoId");
                });

            migrationBuilder.CreateTable(
                name: "Dispositivos",
                columns: table => new
                {
                    DispositivoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MobiControlDeviceId = table.Column<string>(type: "varchar(100)", nullable: false),
                    Fabricante = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Modelo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IMEI = table.Column<string>(type: "varchar(50)", nullable: true),
                    ICCID = table.Column<string>(type: "varchar(50)", nullable: true),
                    NumeroCelular = table.Column<string>(type: "varchar(30)", nullable: true),
                    CostoEquipo = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    EstadoActualId = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dispositivos", x => x.DispositivoId);
                    table.ForeignKey(
                        name: "FK_Dispositivos_EstadosDispositivo_EstadoActualId",
                        column: x => x.EstadoActualId,
                        principalTable: "EstadosDispositivo",
                        principalColumn: "EstadoId");
                });

            migrationBuilder.CreateTable(
                name: "EntregasDispositivo",
                columns: table => new
                {
                    EntregaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntregaUid = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    DispositivoId = table.Column<int>(type: "int", nullable: false),
                    EmpleadoId = table.Column<int>(type: "int", nullable: false),
                    EstadoId = table.Column<int>(type: "int", nullable: true),
                    CanalId = table.Column<int>(type: "int", nullable: true),
                    DistritoId = table.Column<int>(type: "int", nullable: true),
                    NombreAsociadoFirmante = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Entregables = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ICCID = table.Column<string>(type: "varchar(50)", nullable: true),
                    NumeroCelular = table.Column<string>(type: "varchar(30)", nullable: true),
                    CostoEquipo = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    CiudadFirma = table.Column<string>(type: "varchar(100)", nullable: false, defaultValue: "Cali"),
                    FechaEntregaProgramada = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaFirma = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    EstadoProceso = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "FIRMADO"),
                    ClaveIdempotencia = table.Column<string>(type: "varchar(100)", nullable: true),
                    IPOrigen = table.Column<string>(type: "varchar(45)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntregasDispositivo", x => x.EntregaId);
                    table.CheckConstraint("CK_EntregasDispositivo_EstadoProceso", "[EstadoProceso] IN ('FIRMADO','SINCRONIZADO','ERROR_SINCRONIZACION')");
                    table.ForeignKey(
                        name: "FK_EntregasDispositivo_Canales_CanalId",
                        column: x => x.CanalId,
                        principalTable: "Canales",
                        principalColumn: "CanalId");
                    table.ForeignKey(
                        name: "FK_EntregasDispositivo_Dispositivos_DispositivoId",
                        column: x => x.DispositivoId,
                        principalTable: "Dispositivos",
                        principalColumn: "DispositivoId");
                    table.ForeignKey(
                        name: "FK_EntregasDispositivo_Distritos_DistritoId",
                        column: x => x.DistritoId,
                        principalTable: "Distritos",
                        principalColumn: "DistritoId");
                    table.ForeignKey(
                        name: "FK_EntregasDispositivo_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Empleados",
                        principalColumn: "EmpleadoId");
                    table.ForeignKey(
                        name: "FK_EntregasDispositivo_EstadosDispositivo_EstadoId",
                        column: x => x.EstadoId,
                        principalTable: "EstadosDispositivo",
                        principalColumn: "EstadoId");
                });

            migrationBuilder.CreateTable(
                name: "DocumentosPDF",
                columns: table => new
                {
                    DocumentoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntregaId = table.Column<int>(type: "int", nullable: false),
                    NombreContenedor = table.Column<string>(type: "varchar(100)", nullable: false, defaultValue: "documentos-pdf"),
                    RutaBlob = table.Column<string>(type: "varchar(500)", nullable: false),
                    UrlBlob = table.Column<string>(type: "varchar(1000)", nullable: true),
                    NombreArchivo = table.Column<string>(type: "varchar(255)", nullable: false, defaultValue: "firmaentregadispositivo.pdf"),
                    TamanoBytes = table.Column<int>(type: "int", nullable: true),
                    HashSHA256 = table.Column<byte[]>(type: "varbinary(32)", nullable: true),
                    FechaGeneracion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosPDF", x => x.DocumentoId);
                    table.ForeignKey(
                        name: "FK_DocumentosPDF_EntregasDispositivo_EntregaId",
                        column: x => x.EntregaId,
                        principalTable: "EntregasDispositivo",
                        principalColumn: "EntregaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Firmas",
                columns: table => new
                {
                    FirmaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntregaId = table.Column<int>(type: "int", nullable: false),
                    NombreContenedor = table.Column<string>(type: "varchar(100)", nullable: false, defaultValue: "firmas"),
                    RutaBlob = table.Column<string>(type: "varchar(500)", nullable: false),
                    UrlBlob = table.Column<string>(type: "varchar(1000)", nullable: true),
                    FormatoImagen = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "image/png"),
                    TamanoBytes = table.Column<int>(type: "int", nullable: true),
                    HashSHA256 = table.Column<byte[]>(type: "varbinary(32)", nullable: true),
                    FechaCaptura = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Firmas", x => x.FirmaId);
                    table.ForeignKey(
                        name: "FK_Firmas_EntregasDispositivo_EntregaId",
                        column: x => x.EntregaId,
                        principalTable: "EntregasDispositivo",
                        principalColumn: "EntregaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntegracionesSincronizaciones",
                columns: table => new
                {
                    SincronizacionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntregaId = table.Column<int>(type: "int", nullable: false),
                    Proveedor = table.Column<string>(type: "varchar(30)", nullable: false),
                    TipoAccion = table.Column<string>(type: "varchar(50)", nullable: false),
                    Exitoso = table.Column<bool>(type: "bit", nullable: false),
                    CodigoRespuestaHttp = table.Column<int>(type: "int", nullable: true),
                    MensajeError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaEjecucion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegracionesSincronizaciones", x => x.SincronizacionId);
                    table.CheckConstraint("CK_IntegracionesSincronizaciones_Proveedor", "[Proveedor] IN ('MOBICONTROL','INFOBIP','GUPSHUP')");
                    table.ForeignKey(
                        name: "FK_IntegracionesSincronizaciones_EntregasDispositivo_EntregaId",
                        column: x => x.EntregaId,
                        principalTable: "EntregasDispositivo",
                        principalColumn: "EntregaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Canales_Nombre",
                table: "Canales",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_EstadoActualId",
                table: "Dispositivos",
                column: "EstadoActualId");

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
                name: "IX_Distritos_Nombre",
                table: "Distritos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosPDF_EntregaId",
                table: "DocumentosPDF",
                column: "EntregaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Empleados_CanalId",
                table: "Empleados",
                column: "CanalId");

            migrationBuilder.CreateIndex(
                name: "IX_Empleados_Cedula",
                table: "Empleados",
                column: "Cedula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Empleados_DistritoId",
                table: "Empleados",
                column: "DistritoId");

            migrationBuilder.CreateIndex(
                name: "IX_EntregasDispositivo_CanalId",
                table: "EntregasDispositivo",
                column: "CanalId");

            migrationBuilder.CreateIndex(
                name: "IX_EntregasDispositivo_Dispositivo",
                table: "EntregasDispositivo",
                column: "DispositivoId");

            migrationBuilder.CreateIndex(
                name: "IX_EntregasDispositivo_DistritoId",
                table: "EntregasDispositivo",
                column: "DistritoId");

            migrationBuilder.CreateIndex(
                name: "IX_EntregasDispositivo_Empleado",
                table: "EntregasDispositivo",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_EntregasDispositivo_EntregaUid",
                table: "EntregasDispositivo",
                column: "EntregaUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntregasDispositivo_EstadoId",
                table: "EntregasDispositivo",
                column: "EstadoId");

            migrationBuilder.CreateIndex(
                name: "IX_EntregasDispositivo_EstadoProceso",
                table: "EntregasDispositivo",
                column: "EstadoProceso");

            migrationBuilder.CreateIndex(
                name: "IX_EntregasDispositivo_Fecha",
                table: "EntregasDispositivo",
                column: "FechaFirma");

            migrationBuilder.CreateIndex(
                name: "UQ_EntregasDispositivo_Idempotencia",
                table: "EntregasDispositivo",
                column: "ClaveIdempotencia",
                unique: true,
                filter: "[ClaveIdempotencia] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EstadosDispositivo_Nombre",
                table: "EstadosDispositivo",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Firmas_EntregaId",
                table: "Firmas",
                column: "EntregaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_IntegracionesConfig_Proveedor_Entorno",
                table: "IntegracionesConfiguracion",
                columns: new[] { "Proveedor", "Entorno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegracionesSync_Entrega",
                table: "IntegracionesSincronizaciones",
                column: "EntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegracionesSync_Proveedor",
                table: "IntegracionesSincronizaciones",
                column: "Proveedor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentosPDF");

            migrationBuilder.DropTable(
                name: "Firmas");

            migrationBuilder.DropTable(
                name: "IntegracionesConfiguracion");

            migrationBuilder.DropTable(
                name: "IntegracionesSincronizaciones");

            migrationBuilder.DropTable(
                name: "EntregasDispositivo");

            migrationBuilder.DropTable(
                name: "Dispositivos");

            migrationBuilder.DropTable(
                name: "Empleados");

            migrationBuilder.DropTable(
                name: "EstadosDispositivo");

            migrationBuilder.DropTable(
                name: "Canales");

            migrationBuilder.DropTable(
                name: "Distritos");
        }
    }
}
