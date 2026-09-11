using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobiControlFirma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActivarUsuariosPrevios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            // La columna Activo se agregó en la migración anterior con valor por defecto false:
            // EF no conoce el inicializador "= true" de la propiedad cuando rellena filas que ya
            // existían. Resultado: todos los usuarios anteriores quedaron desactivados y la
            // guarda de ingreso los dejó fuera.
            //
            // Se corrigen solo esas filas, identificadas por su FechaCreacion sin valor real,
            // para no reactivar a quien alguien desactive a propósito más adelante.
            migrationBuilder.Sql(@"
                UPDATE [AspNetUsers]
                   SET [Activo] = 1,
                       [FechaCreacion] = SYSUTCDATETIME()
                 WHERE [FechaCreacion] < '2000-01-01';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
