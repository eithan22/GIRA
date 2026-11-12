using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using GIRA.Infrastructure.Services.Reservas;
using GIRA.Tests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Entorno compartido de las pruebas de los procesos automáticos (GI-161 y GI-224): repositorios reales sobre
/// EF Core InMemory y un reloj que solo avanza cuando la prueba lo ordena. Siembra reservas en la franja que
/// empieza a la hora indicada (y termina dos horas después).
/// </summary>
internal sealed class EntornoProceso : IDisposable
{
    private readonly Dictionary<(int, int), FranjaHoraria> _franjas = new();
    private Mesa? _mesa;
    private Cliente? _cliente;

    public GiraDbContext Contexto { get; }
    public TimeProviderAvanzable Reloj { get; }
    public ProcesoAutomaticoReservasService Servicio { get; }
    public PublisherFalso Publisher { get; } = new();
    public DbContextOptions<GiraDbContext> Opciones { get; }

    public EntornoProceso(
        DateTimeOffset ahoraUtc, IPublisher? publisher = null, Func<IReservaRepository, IReservaRepository>? envolver = null)
    {
        Opciones = new DbContextOptionsBuilder<GiraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Contexto = new GiraDbContext(Opciones);
        Reloj = new TimeProviderAvanzable(ahoraUtc);

        IReservaRepository reservas = new ReservaRepository(Contexto);
        if (envolver is not null)
        {
            reservas = envolver(reservas);
        }
        publisher ??= Publisher;

        Servicio = new ProcesoAutomaticoReservasService(
            reservas,
            new ParametrosReservaFalso(),
            new PublicadorEventosSeguro(publisher, NullLogger<PublicadorEventosSeguro>.Instance),
            Reloj);
    }

    public async Task<Reserva> SembrarAsync(
        DateOnly fecha, int hora = 12, int minuto = 0, EstadoReserva estado = EstadoReserva.Confirmada, int personas = 2,
        int? personasReales = null)
    {
        if (_mesa is null)
        {
            var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Salón" };
            _mesa = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 8, ZonaId = zona.Id };
            _cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Ejemplo", Telefono = "8095550101" };
            Contexto.Zonas.Add(zona);
            Contexto.Mesas.Add(_mesa);
            Contexto.Clientes.Add(_cliente);
        }

        if (!_franjas.TryGetValue((hora, minuto), out var franja))
        {
            franja = new FranjaHoraria
            {
                Id = Guid.NewGuid(), Nombre = $"Franja {hora:00}:{minuto:00}",
                HoraInicio = new TimeOnly(hora, minuto), HoraFin = new TimeOnly(hora, minuto).AddHours(2)
            };
            _franjas[(hora, minuto)] = franja;
            Contexto.FranjasHorarias.Add(franja);
        }

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(), Codigo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), ClienteId = _cliente!.Id,
            MesaId = _mesa.Id, FranjaHorariaId = franja.Id, Fecha = fecha, Personas = personas, Origen = OrigenReserva.Telefono
        };
        switch (estado)
        {
            case EstadoReserva.EnCurso:
                reserva.HacerCheckIn(personasReales ?? personas, DateTime.UtcNow);
                break;
            case EstadoReserva.Completada:
                reserva.HacerCheckIn(personasReales ?? personas, DateTime.UtcNow);
                reserva.Completar();
                break;
            case EstadoReserva.Cancelada:
                reserva.Cancelar("Antes");
                break;
            case EstadoReserva.NoAsistio:
                reserva.MarcarNoAsistio();
                break;
        }
        Contexto.Reservas.Add(reserva);
        await Contexto.SaveChangesAsync();
        return reserva;
    }

    public Task<EstadoReserva> EstadoDeAsync(Guid id) =>
        Contexto.Reservas.AsNoTracking().Where(r => r.Id == id).Select(r => r.Estado).SingleAsync();

    public void Dispose() => Contexto.Dispose();
}

