using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inventario_AlertaPorLote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LoteInsumoId",
                schema: "inventario",
                table: "AlertaInventario",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertaInventario_LoteInsumoId",
                schema: "inventario",
                table: "AlertaInventario",
                column: "LoteInsumoId");

            migrationBuilder.AddForeignKey(
                name: "FK_AlertaInventario_LoteInsumo_LoteInsumoId",
                schema: "inventario",
                table: "AlertaInventario",
                column: "LoteInsumoId",
                principalSchema: "inventario",
                principalTable: "LoteInsumo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlertaInventario_LoteInsumo_LoteInsumoId",
                schema: "inventario",
                table: "AlertaInventario");

            migrationBuilder.DropIndex(
                name: "IX_AlertaInventario_LoteInsumoId",
                schema: "inventario",
                table: "AlertaInventario");

            migrationBuilder.DropColumn(
                name: "LoteInsumoId",
                schema: "inventario",
                table: "AlertaInventario");
        }
    }
}
