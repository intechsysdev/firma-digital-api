using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobiControlFirma.Infrastructure.Persistence.Migrations;

/// <summary>
/// Vista de consulta del esquema original. El API no la usa (arma sus propias proyecciones),
/// pero se mantiene porque es la puerta de entrada de los reportes en Excel y Power BI: expone
/// el acta legible sin dejar ver hashes ni rutas internas del almacenamiento.
/// </summary>
public partial class VistaEntregasCompletas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER VIEW vw_EntregasCompletas AS
            SELECT
                e.EntregaId,
                e.EntregaUid,
                d.MobiControlDeviceId,
                d.Fabricante,
                d.Modelo,
                d.IMEI,
                emp.Cedula,
                emp.NombreCompleto,
                e.NombreAsociadoFirmante,
                est.Nombre  AS Estado,
                can.Nombre  AS Canal,
                dist.Nombre AS Distrito,
                e.ICCID,
                e.NumeroCelular,
                e.CostoEquipo,
                e.CiudadFirma,
                e.FechaEntregaProgramada,
                e.FechaFirma,
                e.EstadoProceso,
                f.UrlBlob   AS UrlFirma,
                pdf.UrlBlob AS UrlDocumentoPDF
            FROM EntregasDispositivo e
            JOIN Dispositivos d              ON d.DispositivoId = e.DispositivoId
            JOIN Empleados emp               ON emp.EmpleadoId = e.EmpleadoId
            LEFT JOIN EstadosDispositivo est ON est.EstadoId = e.EstadoId
            LEFT JOIN Canales can            ON can.CanalId = e.CanalId
            LEFT JOIN Distritos dist         ON dist.DistritoId = e.DistritoId
            LEFT JOIN Firmas f               ON f.EntregaId = e.EntregaId
            LEFT JOIN DocumentosPDF pdf      ON pdf.EntregaId = e.EntregaId;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP VIEW IF EXISTS vw_EntregasCompletas;");
    }
}
