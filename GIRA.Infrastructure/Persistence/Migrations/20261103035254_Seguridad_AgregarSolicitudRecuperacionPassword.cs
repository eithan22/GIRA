using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Seguridad_AgregarSolicitudRecuperacionPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolicitudRecuperacionPassword",
                schema: "seguridad",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudRecuperacionPassword", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudRecuperacionPassword_UsuarioId",
                schema: "seguridad",
                table: "SolicitudRecuperacionPassword",
                column: "UsuarioId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SolicitudRecuperacionPassword",
                schema: "seguridad");
        }
    }
}
