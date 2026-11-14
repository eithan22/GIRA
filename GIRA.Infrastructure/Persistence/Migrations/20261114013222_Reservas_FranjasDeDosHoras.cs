using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GIRA.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// GI-226: las franjas por defecto pasan a ser de 2 horas (Almuerzo 12:00–13:45, Tarde 14:00–15:45, Cena
    /// 18:30–20:15 y Noche 20:30–22:15) y dejan de sembrarse con <c>HasData</c>.
    /// </summary>
    /// <remarks>
    /// Quitar <c>HasData</c> generaba un <c>DeleteData</c> de las dos franjas originales, que borraría datos y
    /// rompería las claves foráneas de las reservas; esta migración NO borra nada. En su lugar:
    /// <list type="bullet">
    /// <item>Si la tabla está vacía, inserta las 4 franjas.</item>
    /// <item>Si solo están las 2 originales, sin editar y sin ninguna reserva ni visita que las use (una base
    /// nueva o sin uso), las actualiza a las horas nuevas e inserta Tarde y Noche.</item>
    /// <item>En cualquier otro caso (hay reservas, visitas o ediciones del Gerente) no toca nada: la instalación
    /// conserva sus franjas y el Gerente las ajusta desde Configuración.</item>
    /// </list>
    /// Los valores se repiten aquí a propósito: una migración no debe depender del código que cambie después.
    /// </remarks>
    public partial class Reservas_FranjasDeDosHoras : Migration
    {
        private const string Almuerzo = "00000000-0000-0000-0000-000000000001";
        private const string Cena = "00000000-0000-0000-0000-000000000002";
        private const string Tarde = "00000000-0000-0000-0000-000000000003";
        private const string Noche = "00000000-0000-0000-0000-000000000004";

        // Las dos franjas originales, exactamente como las sembró Reservas_CrearTablas y sin editar.
        private const string OriginalesSinTocar = $@"
(SELECT COUNT(*) FROM [reservas].[FranjaHoraria]) = 2
AND EXISTS (SELECT 1 FROM [reservas].[FranjaHoraria]
    WHERE [Id] = '{Almuerzo}' AND [Nombre] = N'Almuerzo' AND [HoraInicio] = '12:00:00' AND [HoraFin] = '15:00:00'
      AND [Activa] = 1 AND [IsDeleted] = 0 AND [UpdatedAt] IS NULL)
AND EXISTS (SELECT 1 FROM [reservas].[FranjaHoraria]
    WHERE [Id] = '{Cena}' AND [Nombre] = N'Cena' AND [HoraInicio] = '18:00:00' AND [HoraFin] = '22:30:00'
      AND [Activa] = 1 AND [IsDeleted] = 0 AND [UpdatedAt] IS NULL)";

        // Las cuatro franjas nuevas, exactamente como las deja Up y sin editar.
        private const string NuevasSinTocar = $@"
(SELECT COUNT(*) FROM [reservas].[FranjaHoraria]) = 4
AND EXISTS (SELECT 1 FROM [reservas].[FranjaHoraria]
    WHERE [Id] = '{Almuerzo}' AND [Nombre] = N'Almuerzo' AND [HoraInicio] = '12:00:00' AND [HoraFin] = '13:45:00'
      AND [Activa] = 1 AND [IsDeleted] = 0 AND [UpdatedAt] IS NULL)
AND EXISTS (SELECT 1 FROM [reservas].[FranjaHoraria]
    WHERE [Id] = '{Tarde}' AND [Nombre] = N'Tarde' AND [HoraInicio] = '14:00:00' AND [HoraFin] = '15:45:00'
      AND [Activa] = 1 AND [IsDeleted] = 0 AND [UpdatedAt] IS NULL)
AND EXISTS (SELECT 1 FROM [reservas].[FranjaHoraria]
    WHERE [Id] = '{Cena}' AND [Nombre] = N'Cena' AND [HoraInicio] = '18:30:00' AND [HoraFin] = '20:15:00'
      AND [Activa] = 1 AND [IsDeleted] = 0 AND [UpdatedAt] IS NULL)
AND EXISTS (SELECT 1 FROM [reservas].[FranjaHoraria]
    WHERE [Id] = '{Noche}' AND [Nombre] = N'Noche' AND [HoraInicio] = '20:30:00' AND [HoraFin] = '22:15:00'
      AND [Activa] = 1 AND [IsDeleted] = 0 AND [UpdatedAt] IS NULL)";

        // Ninguna reserva ni visita (ni siquiera borrada lógicamente) usa estas franjas.
        private static string SinUso(params string[] ids)
        {
            var lista = string.Join(", ", ids.Select(id => $"'{id}'"));
            return $@"
AND NOT EXISTS (SELECT 1 FROM [reservas].[Reserva] WHERE [FranjaHorariaId] IN ({lista}))
AND NOT EXISTS (SELECT 1 FROM [reservas].[VisitaSinReserva] WHERE [FranjaHorariaId] IN ({lista}))";
        }

        private static string Insertar(string id, string nombre, string inicio, string fin) => $@"
INSERT INTO [reservas].[FranjaHoraria] ([Id], [Nombre], [HoraInicio], [HoraFin], [Activa], [CreatedAt], [IsDeleted])
VALUES ('{id}', N'{nombre}', '{inicio}', '{fin}', 1, SYSUTCDATETIME(), 0);";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Caso 1: tabla vacía -> las 4 franjas nuevas.
            migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM [reservas].[FranjaHoraria])
BEGIN
{Insertar(Almuerzo, "Almuerzo", "12:00:00", "13:45:00")}
{Insertar(Tarde, "Tarde", "14:00:00", "15:45:00")}
{Insertar(Cena, "Cena", "18:30:00", "20:15:00")}
{Insertar(Noche, "Noche", "20:30:00", "22:15:00")}
END");

            // Caso 2: solo las 2 originales, sin editar y sin uso -> se actualizan y se agregan Tarde y Noche.
            // Cualquier otra situación queda intacta.
            migrationBuilder.Sql($@"
IF {OriginalesSinTocar}
{SinUso(Almuerzo, Cena)}
BEGIN
    UPDATE [reservas].[FranjaHoraria] SET [HoraFin] = '13:45:00' WHERE [Id] = '{Almuerzo}';
    UPDATE [reservas].[FranjaHoraria] SET [HoraInicio] = '18:30:00', [HoraFin] = '20:15:00' WHERE [Id] = '{Cena}';
{Insertar(Tarde, "Tarde", "14:00:00", "15:45:00")}
{Insertar(Noche, "Noche", "20:30:00", "22:15:00")}
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo revierte lo que Up cambió, y únicamente si las 4 franjas siguen exactamente como las dejó y
            // nadie las usa: así no se pierden reservas ni ediciones. En cualquier otro caso no toca nada.
            migrationBuilder.Sql($@"
IF {NuevasSinTocar}
{SinUso(Almuerzo, Tarde, Cena, Noche)}
BEGIN
    DELETE FROM [reservas].[FranjaHoraria] WHERE [Id] IN ('{Tarde}', '{Noche}');
    UPDATE [reservas].[FranjaHoraria] SET [HoraFin] = '15:00:00' WHERE [Id] = '{Almuerzo}';
    UPDATE [reservas].[FranjaHoraria] SET [HoraInicio] = '18:00:00', [HoraFin] = '22:30:00' WHERE [Id] = '{Cena}';
END");
        }
    }
}
