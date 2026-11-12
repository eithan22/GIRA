using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Reservas_AgregarIndicesSalon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Zona_Nombre",
                schema: "reservas",
                table: "Zona",
                column: "Nombre",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Mesa_Numero",
                schema: "reservas",
                table: "Mesa",
                column: "Numero",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FranjaHoraria_Nombre",
                schema: "reservas",
                table: "FranjaHoraria",
                column: "Nombre",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Zona_Nombre",
                schema: "reservas",
                table: "Zona");

            migrationBuilder.DropIndex(
                name: "IX_Mesa_Numero",
                schema: "reservas",
                table: "Mesa");

            migrationBuilder.DropIndex(
                name: "IX_FranjaHoraria_Nombre",
                schema: "reservas",
                table: "FranjaHoraria");
        }
    }
}
