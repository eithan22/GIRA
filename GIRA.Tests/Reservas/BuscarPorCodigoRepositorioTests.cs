using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas del repositorio real (EF Core InMemory) para la búsqueda por código (GI-401): el detalle por código es
/// exactamente el mismo armado que el detalle por id, sin depender de la fecha ni del estado.
/// </summary>
public class BuscarPorCodigoRepositorioTests : IDisposable
{
    private readonly GiraDbContext _contexto;
    private readonly ReservaRepository _repositorio;
    private readonly Zona _zona = new() { Id = Guid.NewGuid(), Nombre = "Terraza" };
    private readonly Mesa _mesa;
    private readonly FranjaHoraria _franja = new()
    {
        Id = Guid.NewGuid(), Nombre = "Cena", HoraInicio = new TimeOnly(18, 0), HoraFin = new TimeOnly(22, 30)
    };
    private readonly Cliente _cliente = new() { Id = Guid.NewGuid(), Nombre = "Luis Mota", Telefono = "8295550123" };

    public BuscarPorCodigoRepositorioTests()
    {
        var opciones = new DbContextOptionsBuilder<GiraDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _contexto = new GiraDbContext(opciones);
        _repositorio = new ReservaRepository(_contexto);
        _mesa = new Mesa { Id = Guid.NewGuid(), Numero = 12, Capacidad = 6, ZonaId = _zona.Id };
        _contexto.AddRange(_zona, _mesa, _franja, _cliente);
    }

    public void Dispose() => _contexto.Dispose();

    private async Task<Reserva> SembrarAsync(string codigo, DateOnly fecha, Action<Reserva>? ajustar = null)
    {
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(), Codigo = codigo, ClienteId = _cliente.Id, MesaId = _mesa.Id, FranjaHorariaId = _franja.Id,
            Fecha = fecha, Personas = 4, Origen = OrigenReserva.Presencial
        };
        ajustar?.Invoke(reserva);
        _contexto.Reservas.Add(reserva);
        await _contexto.SaveChangesAsync();
        return reserva;
    }

    [Fact]
    public async Task PorCodigo_Devuelve_El_Mismo_Detalle_Que_Por_Id()
    {
        var reserva = await SembrarAsync("00001234", new DateOnly(2026, 12, 20));

        var porCodigo = await _repositorio.ObtenerDetallePorCodigoAsync("00001234", default);
        var porId = await _repositorio.ObtenerDetalleAsync(reserva.Id, default);

        Assert.NotNull(porCodigo);
        Assert.Equal(porId, porCodigo);
        Assert.Equal("Terraza", porCodigo.ZonaNombre);
        Assert.Equal(12, porCodigo.MesaNumero);
        Assert.Equal("8295550123", porCodigo.ClienteTelefono);
        Assert.Equal("Presencial", porCodigo.Origen);
    }

    [Fact]
    public async Task PorCodigo_Encuentra_Reservas_De_Cualquier_Fecha_Pasada_O_Futura()
    {
        await SembrarAsync("11111111", new DateOnly(2020, 1, 15));
        await SembrarAsync("22222222", new DateOnly(2099, 6, 1));

        Assert.NotNull(await _repositorio.ObtenerDetallePorCodigoAsync("11111111", default));
        Assert.NotNull(await _repositorio.ObtenerDetallePorCodigoAsync("22222222", default));
    }

    [Fact]
    public async Task PorCodigo_Encuentra_Reservas_De_Cualquier_Estado()
    {
        await SembrarAsync("30000001", new DateOnly(2026, 12, 1), r => r.Cancelar("Cambio de planes"));
        await SembrarAsync("30000002", new DateOnly(2026, 12, 1), r => r.MarcarNoAsistio());

        var cancelada = await _repositorio.ObtenerDetallePorCodigoAsync("30000001", default);
        var noAsistio = await _repositorio.ObtenerDetallePorCodigoAsync("30000002", default);

        Assert.Equal("Cancelada", cancelada!.Estado);
        Assert.Equal("Cambio de planes", cancelada.MotivoCancelacion);
        Assert.Equal("NoAsistio", noAsistio!.Estado);
    }

    [Fact]
    public async Task PorCodigo_Elige_Exactamente_El_Codigo_Pedido_Y_Si_No_Existe_Devuelve_Null()
    {
        await SembrarAsync("40000001", new DateOnly(2026, 12, 1));
        await SembrarAsync("40000002", new DateOnly(2026, 12, 1));

        var segunda = await _repositorio.ObtenerDetallePorCodigoAsync("40000002", default);
        var ninguna = await _repositorio.ObtenerDetallePorCodigoAsync("40000003", default);

        Assert.Equal("40000002", segunda!.Codigo);
        Assert.Null(ninguna);
    }
}
