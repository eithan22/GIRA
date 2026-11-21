using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inventario_PrediccionVisitaYParametros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VisitaId",
                schema: "inventario",
                table: "MovimientoInventario",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiasAvisoVencimientoInventario",
                schema: "configuracion",
                table: "ConfiguracionRestaurante",
                type: "int",
                nullable: false,
                // La fila existente toma el valor por defecto del SRS (RN-INV-11): 2 días.
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "DiasProyeccionInventario",
                schema: "configuracion",
                table: "ConfiguracionRestaurante",
                type: "int",
                nullable: false,
                // La fila existente toma el valor por defecto del SRS (5.6): 3 días.
                defaultValue: 3);

            migrationBuilder.CreateTable(
                name: "AfluenciaPredicha",
                schema: "inventario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Comensales = table.Column<int>(type: "int", nullable: false),
                    FechaRecepcion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AfluenciaPredicha", x => x.Id);
                    table.CheckConstraint("CK_AfluenciaPredicha_Comensales", "[Comensales] >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoInventario_VisitaId_InsumoId",
                schema: "inventario",
                table: "MovimientoInventario",
                columns: new[] { "VisitaId", "InsumoId" },
                unique: true,
                filter: "[VisitaId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionRestaurante_DiasAvisoVencimiento",
                schema: "configuracion",
                table: "ConfiguracionRestaurante",
                sql: "[DiasAvisoVencimientoInventario] BETWEEN 1 AND 30");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfiguracionRestaurante_DiasProyeccionInventario",
                schema: "configuracion",
                table: "ConfiguracionRestaurante",
                sql: "[DiasProyeccionInventario] BETWEEN 1 AND 14");

            migrationBuilder.CreateIndex(
                name: "IX_AfluenciaPredicha_Fecha",
                schema: "inventario",
                table: "AfluenciaPredicha",
                column: "Fecha",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AfluenciaPredicha",
                schema: "inventario");

            migrationBuilder.DropIndex(
                name: "IX_MovimientoInventario_VisitaId_InsumoId",
                schema: "inventario",
                table: "MovimientoInventario");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionRestaurante_DiasAvisoVencimiento",
                schema: "configuracion",
                table: "ConfiguracionRestaurante");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfiguracionRestaurante_DiasProyeccionInventario",
                schema: "configuracion",
                table: "ConfiguracionRestaurante");

            migrationBuilder.DropColumn(
                name: "VisitaId",
                schema: "inventario",
                table: "MovimientoInventario");

            migrationBuilder.DropColumn(
                name: "DiasAvisoVencimientoInventario",
                schema: "configuracion",
                table: "ConfiguracionRestaurante");

            migrationBuilder.DropColumn(
                name: "DiasProyeccionInventario",
                schema: "configuracion",
                table: "ConfiguracionRestaurante");
        }
    }
}
