using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnaliticaFuentesKpi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "personal");

            migrationBuilder.AddColumn<decimal>(
                name: "CostoUnitario",
                schema: "inventario",
                table: "LoteInsumo",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CapacidadOperativaDiaria",
                schema: "analitica",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    CapacidadTotal = table.Column<int>(type: "int", nullable: false),
                    CapturadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapacidadOperativaDiaria", x => x.Id);
                    table.CheckConstraint("CK_CapacidadOperativaDiaria_Capacidad", "[CapacidadTotal] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "MarcacionTurno",
                schema: "personal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MinutosPausa = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarcacionTurno", x => x.Id);
                    table.CheckConstraint("CK_MarcacionTurno_Pausa", "[MinutosPausa] >= 0");
                    table.CheckConstraint("CK_MarcacionTurno_Rango", "[FinEn] IS NULL OR [FinEn] > [InicioEn]");
                    table.ForeignKey(
                        name: "FK_MarcacionTurno_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalSchema: "seguridad",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapacidadOperativaDiaria_Fecha",
                schema: "analitica",
                table: "CapacidadOperativaDiaria",
                column: "Fecha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarcacionTurno_UsuarioId",
                schema: "personal",
                table: "MarcacionTurno",
                column: "UsuarioId",
                unique: true,
                filter: "[FinEn] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapacidadOperativaDiaria",
                schema: "analitica");

            migrationBuilder.DropTable(
                name: "MarcacionTurno",
                schema: "personal");

            migrationBuilder.DropColumn(
                name: "CostoUnitario",
                schema: "inventario",
                table: "LoteInsumo");
        }
    }
}
