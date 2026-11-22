using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Analitica_PrediccionAfluencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrediccionAfluencia",
                schema: "analitica",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneracionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    FranjaHorariaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AfluenciaEstimada = table.Column<int>(type: "int", nullable: false),
                    LimiteInferior = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LimiteSuperior = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    BajaConfianza = table.Column<bool>(type: "bit", nullable: false),
                    GeneradaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VersionModelo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrediccionAfluencia", x => x.Id);
                    table.CheckConstraint("CK_PrediccionAfluencia_Estimada", "[AfluenciaEstimada] >= 0");
                    table.CheckConstraint("CK_PrediccionAfluencia_Limites", "([LimiteInferior] IS NULL AND [LimiteSuperior] IS NULL) OR ([LimiteInferior] IS NOT NULL AND [LimiteSuperior] IS NOT NULL AND [LimiteInferior] >= 0 AND [LimiteSuperior] >= [LimiteInferior])");
                    table.ForeignKey(
                        name: "FK_PrediccionAfluencia_FranjaHoraria_FranjaHorariaId",
                        column: x => x.FranjaHorariaId,
                        principalSchema: "reservas",
                        principalTable: "FranjaHoraria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrediccionAfluencia_Fecha_FranjaHorariaId",
                schema: "analitica",
                table: "PrediccionAfluencia",
                columns: new[] { "Fecha", "FranjaHorariaId" },
                unique: true,
                filter: "[Vigente] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PrediccionAfluencia_FranjaHorariaId",
                schema: "analitica",
                table: "PrediccionAfluencia",
                column: "FranjaHorariaId");

            migrationBuilder.CreateIndex(
                name: "IX_PrediccionAfluencia_GeneracionId_Fecha_FranjaHorariaId",
                schema: "analitica",
                table: "PrediccionAfluencia",
                columns: new[] { "GeneracionId", "Fecha", "FranjaHorariaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrediccionAfluencia",
                schema: "analitica");
        }
    }
}
