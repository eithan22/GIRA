using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Analitica_EntrenamientoModelos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntrenamientoModelo",
                schema: "analitica",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    SolicitadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IniciadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinalizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntrenamientoModelo", x => x.Id);
                    table.CheckConstraint("CK_EntrenamientoModelo_Estado", "[Estado] BETWEEN 1 AND 4");
                });

            migrationBuilder.CreateTable(
                name: "ModeloAfluencia",
                schema: "analitica",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntrenamientoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FranjaHorariaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntrenamientoDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    EntrenamientoHasta = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidacionDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidacionHasta = table.Column<DateOnly>(type: "date", nullable: false),
                    Mae = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Rmse = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ArchivoModelo = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModeloAfluencia", x => x.Id);
                    table.CheckConstraint("CK_ModeloAfluencia_Fechas", "[EntrenamientoDesde] <= [EntrenamientoHasta] AND [ValidacionDesde] <= [ValidacionHasta] AND [EntrenamientoHasta] < [ValidacionDesde]");
                    table.CheckConstraint("CK_ModeloAfluencia_Metricas", "[Mae] >= 0 AND [Rmse] >= 0");
                    table.ForeignKey(
                        name: "FK_ModeloAfluencia_EntrenamientoModelo_EntrenamientoId",
                        column: x => x.EntrenamientoId,
                        principalSchema: "analitica",
                        principalTable: "EntrenamientoModelo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModeloAfluencia_FranjaHoraria_FranjaHorariaId",
                        column: x => x.FranjaHorariaId,
                        principalSchema: "reservas",
                        principalTable: "FranjaHoraria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditoriaActivacionModelo",
                schema: "analitica",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModeloAnteriorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModeloActivadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EsRollback = table.Column<bool>(type: "bit", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaActivacionModelo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditoriaActivacionModelo_ModeloAfluencia_ModeloActivadoId",
                        column: x => x.ModeloActivadoId,
                        principalSchema: "analitica",
                        principalTable: "ModeloAfluencia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditoriaActivacionModelo_ModeloAfluencia_ModeloAnteriorId",
                        column: x => x.ModeloAnteriorId,
                        principalSchema: "analitica",
                        principalTable: "ModeloAfluencia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResultadoEntrenamientoModelo",
                schema: "analitica",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntrenamientoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FranjaHorariaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModeloId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    ObservacionesEntrenamiento = table.Column<int>(type: "int", nullable: false),
                    ObservacionesValidacion = table.Column<int>(type: "int", nullable: false),
                    Mae = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Rmse = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MaeReferencia = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    VersionModelo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultadoEntrenamientoModelo", x => x.Id);
                    table.CheckConstraint("CK_ResultadoEntrenamientoModelo_Estado", "[Estado] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_ResultadoEntrenamientoModelo_Observaciones", "[ObservacionesEntrenamiento] >= 0 AND [ObservacionesValidacion] >= 0");
                    table.ForeignKey(
                        name: "FK_ResultadoEntrenamientoModelo_EntrenamientoModelo_EntrenamientoId",
                        column: x => x.EntrenamientoId,
                        principalSchema: "analitica",
                        principalTable: "EntrenamientoModelo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResultadoEntrenamientoModelo_ModeloAfluencia_ModeloId",
                        column: x => x.ModeloId,
                        principalSchema: "analitica",
                        principalTable: "ModeloAfluencia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaActivacionModelo_ActivadoEn",
                schema: "analitica",
                table: "AuditoriaActivacionModelo",
                column: "ActivadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaActivacionModelo_ModeloActivadoId",
                schema: "analitica",
                table: "AuditoriaActivacionModelo",
                column: "ModeloActivadoId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaActivacionModelo_ModeloAnteriorId",
                schema: "analitica",
                table: "AuditoriaActivacionModelo",
                column: "ModeloAnteriorId");

            migrationBuilder.CreateIndex(
                name: "IX_EntrenamientoModelo_Estado",
                schema: "analitica",
                table: "EntrenamientoModelo",
                column: "Estado",
                unique: true,
                filter: "[Estado] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_EntrenamientoModelo_SolicitadoEn",
                schema: "analitica",
                table: "EntrenamientoModelo",
                column: "SolicitadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_ModeloAfluencia_EntrenamientoId",
                schema: "analitica",
                table: "ModeloAfluencia",
                column: "EntrenamientoId");

            migrationBuilder.CreateIndex(
                name: "IX_ModeloAfluencia_FranjaHorariaId",
                schema: "analitica",
                table: "ModeloAfluencia",
                column: "FranjaHorariaId",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ModeloAfluencia_FranjaHorariaId_Version",
                schema: "analitica",
                table: "ModeloAfluencia",
                columns: new[] { "FranjaHorariaId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultadoEntrenamientoModelo_EntrenamientoId_FranjaHorariaId",
                schema: "analitica",
                table: "ResultadoEntrenamientoModelo",
                columns: new[] { "EntrenamientoId", "FranjaHorariaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultadoEntrenamientoModelo_ModeloId",
                schema: "analitica",
                table: "ResultadoEntrenamientoModelo",
                column: "ModeloId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriaActivacionModelo",
                schema: "analitica");

            migrationBuilder.DropTable(
                name: "ResultadoEntrenamientoModelo",
                schema: "analitica");

            migrationBuilder.DropTable(
                name: "ModeloAfluencia",
                schema: "analitica");

            migrationBuilder.DropTable(
                name: "EntrenamientoModelo",
                schema: "analitica");
        }
    }
}
