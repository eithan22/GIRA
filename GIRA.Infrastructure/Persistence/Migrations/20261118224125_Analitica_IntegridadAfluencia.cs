using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Analitica_IntegridadAfluencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RegistroAfluencia_FranjaHorariaId",
                schema: "analitica",
                table: "RegistroAfluencia",
                column: "FranjaHorariaId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RegistroAfluencia_Fecha",
                schema: "analitica",
                table: "RegistroAfluencia",
                sql: "[Fecha] > '0001-01-01'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RegistroAfluencia_Personas",
                schema: "analitica",
                table: "RegistroAfluencia",
                sql: "[Personas] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RegistroAfluencia_Referencia",
                schema: "analitica",
                table: "RegistroAfluencia",
                sql: "[ReferenciaId] <> '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RegistroAfluencia_TipoOrigen",
                schema: "analitica",
                table: "RegistroAfluencia",
                sql: "([Tipo] = 1 AND [Origen] IN (1, 2)) OR ([Tipo] = 2 AND [Origen] = 1)");

            migrationBuilder.AddForeignKey(
                name: "FK_RegistroAfluencia_FranjaHoraria_FranjaHorariaId",
                schema: "analitica",
                table: "RegistroAfluencia",
                column: "FranjaHorariaId",
                principalSchema: "reservas",
                principalTable: "FranjaHoraria",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RegistroAfluencia_FranjaHoraria_FranjaHorariaId",
                schema: "analitica",
                table: "RegistroAfluencia");

            migrationBuilder.DropIndex(
                name: "IX_RegistroAfluencia_FranjaHorariaId",
                schema: "analitica",
                table: "RegistroAfluencia");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RegistroAfluencia_Fecha",
                schema: "analitica",
                table: "RegistroAfluencia");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RegistroAfluencia_Personas",
                schema: "analitica",
                table: "RegistroAfluencia");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RegistroAfluencia_Referencia",
                schema: "analitica",
                table: "RegistroAfluencia");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RegistroAfluencia_TipoOrigen",
                schema: "analitica",
                table: "RegistroAfluencia");
        }
    }
}
