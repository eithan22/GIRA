using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Configuracion_CrearTablas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "configuracion");

            migrationBuilder.CreateTable(
                name: "ConfiguracionRestaurante",
                schema: "configuracion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AntelacionMinimaMinutos = table.Column<int>(type: "int", nullable: false),
                    AntelacionMaximaDias = table.Column<int>(type: "int", nullable: false),
                    LimiteCancelacionMinutos = table.Column<int>(type: "int", nullable: false),
                    VentanaCheckInMinutos = table.Column<int>(type: "int", nullable: false),
                    MaxReservasVigentesPorTelefono = table.Column<int>(type: "int", nullable: false),
                    MaxPersonasPorReserva = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ConfiguracionRestaurante", x => x.Id);
                    table.CheckConstraint("CK_ConfiguracionRestaurante_AntelacionMaxima", "[AntelacionMaximaDias] BETWEEN 1 AND 365");
                    table.CheckConstraint("CK_ConfiguracionRestaurante_AntelacionMinima", "[AntelacionMinimaMinutos] >= 0");
                    table.CheckConstraint("CK_ConfiguracionRestaurante_FilaUnica", "[Id] = '00000000-0000-0000-0000-0000000000c1'");
                    table.CheckConstraint("CK_ConfiguracionRestaurante_LimiteCancelacion", "[LimiteCancelacionMinutos] >= 0");
                    table.CheckConstraint("CK_ConfiguracionRestaurante_MaxPersonas", "[MaxPersonasPorReserva] BETWEEN 1 AND 50");
                    table.CheckConstraint("CK_ConfiguracionRestaurante_MaxVigentes", "[MaxReservasVigentesPorTelefono] BETWEEN 1 AND 10");
                    table.CheckConstraint("CK_ConfiguracionRestaurante_VentanaCheckIn", "[VentanaCheckInMinutos] BETWEEN 0 AND 120");
                });

            migrationBuilder.CreateTable(
                name: "DiaCerrado",
                schema: "configuracion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_DiaCerrado", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HorarioOperacion",
                schema: "configuracion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiaSemana = table.Column<int>(type: "int", nullable: false),
                    Abierto = table.Column<bool>(type: "bit", nullable: false),
                    HoraApertura = table.Column<TimeOnly>(type: "time", nullable: false),
                    HoraCierre = table.Column<TimeOnly>(type: "time", nullable: false),
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
                    table.PrimaryKey("PK_HorarioOperacion", x => x.Id);
                    table.CheckConstraint("CK_HorarioOperacion_DiaSemana", "[DiaSemana] BETWEEN 0 AND 6");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiaCerrado_Fecha",
                schema: "configuracion",
                table: "DiaCerrado",
                column: "Fecha",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_HorarioOperacion_DiaSemana",
                schema: "configuracion",
                table: "HorarioOperacion",
                column: "DiaSemana",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionRestaurante",
                schema: "configuracion");

            migrationBuilder.DropTable(
                name: "DiaCerrado",
                schema: "configuracion");

            migrationBuilder.DropTable(
                name: "HorarioOperacion",
                schema: "configuracion");
        }
    }
}
