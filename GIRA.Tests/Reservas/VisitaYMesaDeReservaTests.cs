using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using GIRA.Infrastructure.Services.Reservas;
using GIRA.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Limitación conocida y documentada (GI-226, fase 2): una visita sin reserva NO ocupa la mesa para la asignación
/// de reservas, porque no tiene hora de salida (GI-402). Esta prueba fija el comportamiento actual: si cambia la
/// decisión de producto, esta prueba debe cambiar con ella.
/// </summary>
public class VisitaYMesaDeReservaTests
{
    private static readonly DateOnly Dia = new(2026, 12, 1);

    [Fact]
    public async Task UnaReservaDelPersonalPuedeAsignarseALaMesaDondeHayUnaVisitaSinReservaEnLaMismaFranja()
    {
        var opciones = new DbContextOptionsBuilder<GiraDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiraDbContext(opciones);
        var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Salón" };
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 4, ZonaId = zona.Id }; // la única mesa
        var franja = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Almuerzo", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(13, 45)
        };
        db.Zonas.Add(zona);
        db.Mesas.Add(mesa);
        db.FranjasHorarias.Add(franja);
        db.VisitasSinReserva.Add(new VisitaSinReserva
        {
            Id = Guid.NewGuid(), Fecha = Dia, FranjaHorariaId = franja.Id, MesaId = mesa.Id, PersonasReales = 2
        });
        await db.SaveChangesAsync();

        var reloj = new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero)); // 12:00 local
        var parametros = new ParametrosReservaFalso();
        var publisher = new PublisherFalso();
        var servicio = new ReservaService(
            new MesaRepository(db), new FranjaHorariaRepository(db), new ReservaRepository(db), new ClienteRepository(db),
            new MesaGestionRepository(db), parametros, new CrearReservaRequestValidator(parametros, reloj),
            new ListarReservasRequestValidator(), new CheckInRequestValidator(), new CancelarPorPersonalRequestValidator(),
            new ModificarReservaRequestValidator(), new GeneradorCodigoReserva(), publisher,
            new PublicadorEventosSeguro(publisher, NullLogger<PublicadorEventosSeguro>.Instance), reloj);

        var resultado = await servicio.CrearComoPersonalAsync(
            new CrearReservaRequest("Ana Pérez", "8095551234", Dia, franja.Id, 2), OrigenReserva.Telefono, default);

        // La reserva se acepta y toma la mesa de la visita: el sistema no la considera ocupada.
        Assert.True(resultado.IsSuccess);
        Assert.Equal(mesa.Id, (await db.Reservas.SingleAsync()).MesaId);
    }
}
