using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Seguridad_VincularUsuarioEmpleado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_Usuario_Empleado_EmpleadoId",
                schema: "seguridad",
                table: "Usuario",
                column: "EmpleadoId",
                principalSchema: "personal",
                principalTable: "Empleado",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Usuario_Empleado_EmpleadoId",
                schema: "seguridad",
                table: "Usuario");
        }
    }
}
