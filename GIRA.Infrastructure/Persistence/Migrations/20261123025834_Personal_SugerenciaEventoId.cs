using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Personal_SugerenciaEventoId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventoId",
                schema: "personal",
                table: "SugerenciaTurno",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SugerenciaTurno_EventoId",
                schema: "personal",
                table: "SugerenciaTurno",
                column: "EventoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SugerenciaTurno_EventoId",
                schema: "personal",
                table: "SugerenciaTurno");

            migrationBuilder.DropColumn(
                name: "EventoId",
                schema: "personal",
                table: "SugerenciaTurno");
        }
    }
}
