using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Analitica_RegistroAfluencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "analitica");

            migrationBuilder.CreateTable(
                name: "RegistroAfluencia",
                schema: "analitica",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origen = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    FranjaHorariaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Personas = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistroAfluencia", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistroAfluencia_Fecha_FranjaHorariaId",
                schema: "analitica",
                table: "RegistroAfluencia",
                columns: new[] { "Fecha", "FranjaHorariaId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistroAfluencia_Origen_ReferenciaId",
                schema: "analitica",
                table: "RegistroAfluencia",
                columns: new[] { "Origen", "ReferenciaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistroAfluencia",
                schema: "analitica");
        }
    }
}
