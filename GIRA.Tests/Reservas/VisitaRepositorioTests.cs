using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas del repositorio real (EF Core InMemory) para el listado de visitas sin reserva (GI-402): filtra por fecha,
/// los totales cuentan todo el día, el orden es el más reciente primero con desempate estable, y una visita cuya
/// franja o mesa ya no existe sigue en la lista (left join).
/// </summary>
public class VisitaRepositorioTests : IDisposable
{
    private static readonly DateOnly Dia = new(2026, 12, 1);

    private readonly GiraDbContext _contexto;
    private readonly VisitaRepository _repositorio;
    private readonly Zona _zona = new() { Id = Guid.NewGuid(), Nombre = "Salón" };
    private readonly Mesa _mesa;
    private readonly FranjaHoraria _franja = new()
    {
        Id = Guid.NewGuid(), Nombre = "Almuerzo", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
    };

    public VisitaRepositorioTests()
    {
        var opciones = new DbContextOptionsBuilder<GiraDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _contexto = new GiraDbContext(opciones);
        _repositorio = new VisitaRepository(_contexto);
        _mesa = new Mesa { Id = Guid.NewGuid(), Numero = 7, Capacidad = 6, ZonaId = _zona.Id };
        _contexto.AddRange(_zona, _mesa, _franja);
    }

    public void Dispose() => _contexto.Dispose();

    private async Task<VisitaSinReserva> SembrarAsync(
        int personas, DateTime? creadaUtc = null, DateOnly? fecha = null, bool conMesa = true, Action<VisitaSinReserva>? ajustar = null)
    {
        var visita = new VisitaSinReserva
        {
            Id = Guid.NewGuid(), Fecha = fecha ?? Dia, FranjaHorariaId = _franja.Id, MesaId = conMesa ? _mesa.Id : null,
            PersonasReales = personas,
            // Este contexto no lleva el interceptor de auditoría: sin esto CreatedAt queda en su valor por defecto.
            CreatedAt = creadaUtc ?? default
        };
        ajustar?.Invoke(visita);
        _contexto.VisitasSinReserva.Add(visita);
        await _contexto.SaveChangesAsync();
        return visita;
    }

    private static DateTime Utc(int hora, int minuto) => new(2026, 12, 1, hora, minuto, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Solo_Trae_Las_Visitas_De_La_Fecha_Pedida()
    {
        await SembrarAsync(2, Utc(16, 0));
        await SembrarAsync(3, Utc(16, 5), Dia.AddDays(-1));
        await SembrarAsync(4, Utc(16, 10), Dia.AddDays(1));

        var hoy = await _repositorio.ListarPorFechaAsync(Dia, 200, default);
        var ayer = await _repositorio.ListarPorFechaAsync(Dia.AddDays(-1), 200, default);

        Assert.Equal(1, hoy.TotalVisitas);
        Assert.Equal(2, hoy.TotalPersonas);
        Assert.Equal(3, Assert.Single(ayer.Filas).Personas);
    }

    [Fact]
    public async Task Los_Totales_Cuentan_Todo_El_Dia_Y_La_Lista_Se_Recorta_Al_Limite()
    {
        for (var i = 1; i <= 5; i++)
        {
            await SembrarAsync(i, Utc(16, i));
        }

        var dia = await _repositorio.ListarPorFechaAsync(Dia, 3, default);

        Assert.Equal(5, dia.TotalVisitas);
        Assert.Equal(15, dia.TotalPersonas); // 1+2+3+4+5, no solo las 3 filas devueltas
        Assert.Equal(3, dia.Filas.Count);
    }

    [Fact]
    public async Task Ordena_Las_Mas_Recientes_Primero_Y_Al_Recortar_Se_Quedan_Las_Mas_Recientes()
    {
        var a = await SembrarAsync(1, Utc(16, 0));
        var b = await SembrarAsync(2, Utc(17, 30));
        var c = await SembrarAsync(3, Utc(16, 45));

        var completo = await _repositorio.ListarPorFechaAsync(Dia, 200, default);
        var recortado = await _repositorio.ListarPorFechaAsync(Dia, 2, default);

        Assert.Equal([b.Id, c.Id, a.Id], completo.Filas.Select(f => f.Id));
        Assert.Equal([b.Id, c.Id], recortado.Filas.Select(f => f.Id));
    }

    [Fact]
    public async Task Con_El_Mismo_Momento_De_Registro_El_Orden_Es_Estable_Por_Id_Descendente()
    {
        var momento = Utc(16, 0);
        var visitas = new List<VisitaSinReserva>();
        for (var i = 0; i < 4; i++)
        {
            visitas.Add(await SembrarAsync(2, momento));
        }

        var primera = await _repositorio.ListarPorFechaAsync(Dia, 200, default);
        var segunda = await _repositorio.ListarPorFechaAsync(Dia, 200, default);

        Assert.Equal(visitas.OrderByDescending(v => v.Id).Select(v => v.Id), primera.Filas.Select(f => f.Id));
        Assert.Equal(primera.Filas.Select(f => f.Id), segunda.Filas.Select(f => f.Id));
    }

    [Fact]
    public async Task Resuelve_La_Franja_Y_El_Numero_De_Mesa_O_Deja_La_Mesa_En_Null_Si_No_Tiene()
    {
        await SembrarAsync(2, Utc(16, 0), conMesa: true);
        await SembrarAsync(5, Utc(16, 30), conMesa: false);

        var dia = await _repositorio.ListarPorFechaAsync(Dia, 200, default);

        var sinMesa = dia.Filas[0]; // la más reciente
        var conMesa = dia.Filas[1];
        Assert.Equal("Almuerzo", sinMesa.FranjaNombre);
        Assert.Null(sinMesa.MesaNumero);
        Assert.Equal(7, conMesa.MesaNumero);
    }

    [Fact]
    public async Task Una_Franja_O_Mesa_Borrada_No_Saca_La_Visita_De_La_Lista_Ni_De_Los_Totales()
    {
        await SembrarAsync(4, Utc(16, 0));
        _franja.IsDeleted = true;
        _mesa.IsDeleted = true;
        await _contexto.SaveChangesAsync();

        var dia = await _repositorio.ListarPorFechaAsync(Dia, 200, default);

        Assert.Equal(1, dia.TotalVisitas);
        Assert.Equal(4, dia.TotalPersonas);
        var fila = Assert.Single(dia.Filas);
        Assert.Null(fila.FranjaNombre);
        Assert.Null(fila.MesaNumero);
        Assert.Equal(4, fila.Personas);
    }

    [Fact]
    public async Task Una_Visita_Borrada_Logicamente_No_Aparece_Ni_Cuenta()
    {
        await SembrarAsync(2, Utc(16, 0));
        await SembrarAsync(9, Utc(16, 5), ajustar: v => v.IsDeleted = true);

        var dia = await _repositorio.ListarPorFechaAsync(Dia, 200, default);

        Assert.Equal(1, dia.TotalVisitas);
        Assert.Equal(2, dia.TotalPersonas);
        Assert.Single(dia.Filas);
    }

    [Fact]
    public async Task Un_Dia_Sin_Visitas_Devuelve_Ceros_Y_Lista_Vacia()
    {
        var dia = await _repositorio.ListarPorFechaAsync(Dia, 200, default);

        Assert.Equal(0, dia.TotalVisitas);
        Assert.Equal(0, dia.TotalPersonas);
        Assert.Empty(dia.Filas);
    }

    [Fact]
    public async Task Devuelve_El_Momento_Utc_De_Registro_Tal_Cual()
    {
        var momento = Utc(21, 30);
        await SembrarAsync(2, momento);

        var dia = await _repositorio.ListarPorFechaAsync(Dia, 200, default);

        Assert.Equal(momento, Assert.Single(dia.Filas).CreadaEnUtc);
    }
}
