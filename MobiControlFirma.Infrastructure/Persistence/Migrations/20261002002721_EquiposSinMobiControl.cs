using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobiControlFirma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EquiposSinMobiControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dispositivos_EmpresaId_MobiControlDeviceId",
                table: "Dispositivos");

            migrationBuilder.AlterColumn<string>(
                name: "MobiControlDeviceId",
                table: "Dispositivos",
                type: "varchar(100)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)");

            migrationBuilder.AddColumn<string>(
                name: "Serial",
                table: "Dispositivos",
                type: "varchar(100)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoDispositivo",
                table: "Dispositivos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_EmpresaId_MobiControlDeviceId",
                table: "Dispositivos",
                columns: new[] { "EmpresaId", "MobiControlDeviceId" },
                unique: true,
                filter: "[MobiControlDeviceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_EmpresaId_Serial",
                table: "Dispositivos",
                columns: new[] { "EmpresaId", "Serial" },
                unique: true,
                filter: "[Serial] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dispositivos_EmpresaId_MobiControlDeviceId",
                table: "Dispositivos");

            migrationBuilder.DropIndex(
                name: "IX_Dispositivos_EmpresaId_Serial",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "Serial",
                table: "Dispositivos");

            migrationBuilder.DropColumn(
                name: "TipoDispositivo",
                table: "Dispositivos");

            migrationBuilder.AlterColumn<string>(
                name: "MobiControlDeviceId",
                table: "Dispositivos",
                type: "varchar(100)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dispositivos_EmpresaId_MobiControlDeviceId",
                table: "Dispositivos",
                columns: new[] { "EmpresaId", "MobiControlDeviceId" },
                unique: true);
        }
    }
}
