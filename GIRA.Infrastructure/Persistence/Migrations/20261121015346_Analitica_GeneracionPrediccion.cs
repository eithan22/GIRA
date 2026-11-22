using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Analitica_GeneracionPrediccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeneracionPrediccion",
                schema: "analitica",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaProceso = table.Column<DateOnly>(type: "date", nullable: false),
                    GeneradaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CantidadPredicciones = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneracionPrediccion", x => x.Id);
                    table.CheckConstraint("CK_GeneracionPrediccion_Cantidad", "[CantidadPredicciones] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "PublicacionPrediccion",
                schema: "analitica",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneracionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    PublicadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicacionPrediccion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicacionPrediccion_GeneracionPrediccion_GeneracionId",
                        column: x => x.GeneracionId,
                        principalSchema: "analitica",
                        principalTable: "GeneracionPrediccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeneracionPrediccion_FechaProceso",
                schema: "analitica",
                table: "GeneracionPrediccion",
                column: "FechaProceso",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicacionPrediccion_GeneracionId_Fecha",
                schema: "analitica",
                table: "PublicacionPrediccion",
                columns: new[] { "GeneracionId", "Fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicacionPrediccion_PublicadaEn",
                schema: "analitica",
                table: "PublicacionPrediccion",
                column: "PublicadaEn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicacionPrediccion",
                schema: "analitica");

            migrationBuilder.DropTable(
                name: "GeneracionPrediccion",
                schema: "analitica");
        }
    }
}
