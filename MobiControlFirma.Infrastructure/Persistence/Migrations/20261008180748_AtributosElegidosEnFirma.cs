using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobiControlFirma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AtributosElegidosEnFirma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AtributoFecha",
                table: "Empresas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AtributoFirma",
                table: "Empresas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtributoFecha",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "AtributoFirma",
                table: "Empresas");
        }
    }
}
