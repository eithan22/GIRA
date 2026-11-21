using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Configuracion_AgregarToleranciaYTelefono : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Teléfono de contacto: NULLABLE y sin valor por defecto, para que la fila existente quede en NULL
            // (no se siembra ningún número de ejemplo; lo escribe el restaurante desde Configuración).
            migrationBuilder.AddColumn<string>(
                name: "TelefonoContacto",
                schema: "configuracion",
                table: "ConfiguracionRestaurante",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            // Tolerancia de no-show: NOT NULL con 15 por defecto, para que la fila existente quede con los
            // 15 minutos acordados (GI-404) sin necesidad de un UPDATE aparte.
            migrationBuilder.AddColumn<int>(
                name: "ToleranciaNoShowMinutos",
                schema: "configuracion",
                table: "ConfiguracionRestaurante",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionRestaurante_ToleranciaNoShow",
                schema: "configuracion",
                table: "ConfiguracionRestaurante",
                sql: "[ToleranciaNoShowMinutos] BETWEEN 0 AND 120");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionRestaurante_ToleranciaNoShow",
                schema: "configuracion",
                table: "ConfiguracionRestaurante");

            migrationBuilder.DropColumn(
                name: "TelefonoContacto",
                schema: "configuracion",
                table: "ConfiguracionRestaurante");

            migrationBuilder.DropColumn(
                name: "ToleranciaNoShowMinutos",
                schema: "configuracion",
                table: "ConfiguracionRestaurante");
        }
    }
}
