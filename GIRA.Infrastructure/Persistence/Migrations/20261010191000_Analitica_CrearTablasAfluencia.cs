using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GiraDbContext))]
[Migration("20261010191000_Analitica_CrearTablasAfluencia")]
public partial class Analitica_CrearTablasAfluencia : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "analitica");

        migrationBuilder.CreateTable(
            name: "HistorialAfluencia",
            schema: "analitica",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                HoraInicio = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                CantidadComensales = table.Column<int>(type: "int", nullable: false),
                ReservaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_HistorialAfluencia", row => row.Id));

        migrationBuilder.CreateTable(
            name: "PrediccionResultado",
            schema: "analitica",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                HoraInicio = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                AfluenciaEstimada = table.Column<int>(type: "int", nullable: false),
                FechaGeneracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                Modelo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_PrediccionResultado", row => row.Id));

        migrationBuilder.CreateIndex(
            name: "IX_HistorialAfluencia_Fecha_HoraInicio",
            schema: "analitica",
            table: "HistorialAfluencia",
            columns: new[] { "Fecha", "HoraInicio" });

        migrationBuilder.CreateIndex(
            name: "IX_HistorialAfluencia_ReservaId",
            schema: "analitica",
            table: "HistorialAfluencia",
            column: "ReservaId",
            unique: true,
            filter: "[ReservaId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_PrediccionResultado_Fecha_HoraInicio",
            schema: "analitica",
            table: "PrediccionResultado",
            columns: new[] { "Fecha", "HoraInicio" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "HistorialAfluencia", schema: "analitica");
        migrationBuilder.DropTable(name: "PrediccionResultado", schema: "analitica");
    }
}
