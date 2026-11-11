using System.Text.Json;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using GIRA.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas del mapa de ocupación (GI-167, GI-205): estados de cada mesa, la inactividad que gana sobre la reserva,
/// el desempate EnCurso, el resumen, el orden, el borrado lógico y que el repositorio es solo lectura y no trae el
/// teléfono. Repositorios reales sobre EF Core InMemory.
/// </summary>
public class OcupacionServicioTests
{
    // "Hoy" = 1 de diciembre de 2026 (mediodía en Santo Domingo).
    private static readonly DateOnly Hoy = new(2026, 12, 1);

    private sealed class Entorno : IDisposable
    {
        public GiraDbContext Contexto { get; }
        public OcupacionService Servicio { get; }
        public Zona Salon { get; }
        public Zona Terraza { get; }
        public Zona Cerrada { get; }
        public FranjaHoraria Almuerzo { get; }
        public FranjaHoraria Cena { get; }
        public Mesa Mesa1 { get; }
        public Mesa Mesa2 { get; }
        public Mesa Mesa3InactivaPropia { get; }
        public Mesa Mesa4Terraza { get; }
        public Mesa Mesa5DeZonaInactiva { get; }
        public Mesa Mesa6DeZonaInactiva { get; }
        public Cliente Cliente { get; }

        // Salón (mesas 1, 2 y 3, esta última inactiva), Terraza (mesa 4) y "Cerrada" (zona inactiva, mesas 5 y 6).
        public Entorno()
        {
            var opciones = new DbContextOptionsBuilder<GiraDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Contexto = new GiraDbContext(opciones);
            Servicio = new OcupacionService(
                new OcupacionRepository(Contexto),
                new ConsultarOcupacionRequestValidator(),
                new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero)));

            Salon = new Zona { Id = Guid.NewGuid(), Nombre = "Salón" };
            Terraza = new Zona { Id = Guid.NewGuid(), Nombre = "Terraza" };
            Cerrada = new Zona { Id = Guid.NewGuid(), Nombre = "Cerrada", Activa = false };
            Almuerzo = new FranjaHoraria
            {
                Id = Guid.NewGuid(), Nombre = "Almuerzo", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
            };
            Cena = new FranjaHoraria
            {
                Id = Guid.NewGuid(), Nombre = "Cena", HoraInicio = new TimeOnly(18, 0), HoraFin = new TimeOnly(22, 30)
            };
            Mesa1 = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2, ZonaId = Salon.Id };
            Mesa2 = new Mesa { Id = Guid.NewGuid(), Numero = 2, Capacidad = 4, ZonaId = Salon.Id };
            Mesa3InactivaPropia = new Mesa { Id = Guid.NewGuid(), Numero = 3, Capacidad = 4, ZonaId = Salon.Id, Activa = false };
            Mesa4Terraza = new Mesa { Id = Guid.NewGuid(), Numero = 4, Capacidad = 6, ZonaId = Terraza.Id };
            Mesa5DeZonaInactiva = new Mesa { Id = Guid.NewGuid(), Numero = 5, Capacidad = 4, ZonaId = Cerrada.Id };
            Mesa6DeZonaInactiva = new Mesa { Id = Guid.NewGuid(), Numero = 6, Capacidad = 4, ZonaId = Cerrada.Id };
            Cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Ejemplo", Telefono = "8095550123" };

            Contexto.Zonas.AddRange(Salon, Terraza, Cerrada);
            Contexto.FranjasHorarias.AddRange(Almuerzo, Cena);
            Contexto.Mesas.AddRange(Mesa1, Mesa2, Mesa3InactivaPropia, Mesa4Terraza, Mesa5DeZonaInactiva, Mesa6DeZonaInactiva);
            Contexto.Clientes.Add(Cliente);
            Contexto.SaveChanges();
        }

        public async Task<Reserva> ReservarAsync(
            Mesa mesa, EstadoReserva estado = EstadoReserva.Confirmada, DateOnly? fecha = null,
            FranjaHoraria? franja = null, int personas = 2, int? personasReales = null)
        {
            var reserva = new Reserva
            {
                Id = Guid.NewGuid(), Codigo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), ClienteId = Cliente.Id,
                MesaId = mesa.Id, FranjaHorariaId = (franja ?? Almuerzo).Id, Fecha = fecha ?? Hoy, Personas = personas,
                Origen = OrigenReserva.Telefono
            };
            switch (estado)
            {
                case EstadoReserva.EnCurso:
                    reserva.HacerCheckIn(personasReales ?? personas, DateTime.UtcNow);
                    break;
                case EstadoReserva.Completada:
                    reserva.HacerCheckIn(personas, DateTime.UtcNow);
                    reserva.Completar();
                    break;
                case EstadoReserva.Cancelada:
                    reserva.Cancelar("Prueba");
                    break;
                case EstadoReserva.NoAsistio:
                    reserva.MarcarNoAsistio();
                    break;
            }
            Contexto.Reservas.Add(reserva);
            await Contexto.SaveChangesAsync();
            return reserva;
        }

        public async Task<MapaOcupacionDto> MapaAsync(DateOnly? fecha = null, FranjaHoraria? franja = null)
        {
            var resultado = await Servicio.ObtenerMapaAsync(
                new ConsultarOcupacionRequest { Fecha = (fecha ?? Hoy).ToString("yyyy-MM-dd"), FranjaId = (franja ?? Almuerzo).Id.ToString() },
                default);
            Assert.True(resultado.IsSuccess, resultado.Error);
            return resultado.Value!;
        }

        public void Dispose() => Contexto.Dispose();
    }

    private static MesaOcupacionDto Mesa(MapaOcupacionDto mapa, int numero) =>
        mapa.Zonas.SelectMany(z => z.Mesas).Single(m => m.Numero == numero);

    // --- Estados ---

    [Fact]
    public async Task Sin_Reservas_Las_Mesas_Activas_Estan_Libres_Y_Las_Inactivas_Inactivas()
    {
        using var e = new Entorno();

        var mapa = await e.MapaAsync();

        Assert.Equal("Libre", Mesa(mapa, 1).Estado);
        Assert.Equal("Libre", Mesa(mapa, 2).Estado);
        Assert.Equal("Libre", Mesa(mapa, 4).Estado);
        Assert.Equal("Inactiva", Mesa(mapa, 3).Estado);
        Assert.Equal("Inactiva", Mesa(mapa, 5).Estado);
        Assert.All(mapa.Zonas.SelectMany(z => z.Mesas).Where(m => m.Estado == "Libre"), m => Assert.Null(m.ReservaId));
    }

    [Fact]
    public async Task Una_Reserva_Confirmada_Deja_La_Mesa_Reservada_Con_El_Nombre_Del_Cliente()
    {
        using var e = new Entorno();
        var reserva = await e.ReservarAsync(e.Mesa1, personas: 2);

        var mesa = Mesa(await e.MapaAsync(), 1);

        Assert.Equal("Reservada", mesa.Estado);
        Assert.Equal(reserva.Id, mesa.ReservaId);
        Assert.Equal("Cliente Ejemplo", mesa.ClienteNombre);
        Assert.Equal(2, mesa.Personas);
        Assert.Null(mesa.PersonasReales);
    }

    [Fact]
    public async Task Una_Reserva_EnCurso_Deja_La_Mesa_Ocupada_Con_Las_Personas_Reales()
    {
        using var e = new Entorno();
        var reserva = await e.ReservarAsync(e.Mesa2, EstadoReserva.EnCurso, personas: 3, personasReales: 4);

        var mesa = Mesa(await e.MapaAsync(), 2);

        Assert.Equal("Ocupada", mesa.Estado);
        Assert.Equal(reserva.Id, mesa.ReservaId);
        Assert.Equal(3, mesa.Personas);
        Assert.Equal(4, mesa.PersonasReales);
    }

    [Theory]
    [InlineData(EstadoReserva.Completada)]
    [InlineData(EstadoReserva.Cancelada)]
    [InlineData(EstadoReserva.NoAsistio)]
    public async Task Las_Reservas_Que_Ya_No_Retienen_La_Mesa_La_Dejan_Libre(EstadoReserva estado)
    {
        using var e = new Entorno();
        await e.ReservarAsync(e.Mesa1, estado);

        var mesa = Mesa(await e.MapaAsync(), 1);

        Assert.Equal("Libre", mesa.Estado);
        Assert.Null(mesa.ReservaId);
    }

    [Fact]
    public async Task Las_Reservas_De_Otra_Fecha_O_De_Otra_Franja_No_Cuentan()
    {
        using var e = new Entorno();
        await e.ReservarAsync(e.Mesa1, fecha: Hoy.AddDays(1));
        await e.ReservarAsync(e.Mesa2, franja: e.Cena);

        var mapa = await e.MapaAsync();

        Assert.Equal("Libre", Mesa(mapa, 1).Estado);
        Assert.Equal("Libre", Mesa(mapa, 2).Estado);
        Assert.Equal("Reservada", Mesa(await e.MapaAsync(franja: e.Cena), 2).Estado);
        Assert.Equal("Reservada", Mesa(await e.MapaAsync(fecha: Hoy.AddDays(1)), 1).Estado);
    }

    // --- La inactividad gana (A) ---

    [Fact]
    public async Task Una_Mesa_Inactiva_Con_Reserva_Vigente_Sigue_Inactiva_Pero_Muestra_La_Reserva()
    {
        using var e = new Entorno();
        var reserva = await e.ReservarAsync(e.Mesa3InactivaPropia, personas: 3);

        var mesa = Mesa(await e.MapaAsync(), 3);

        Assert.Equal("Inactiva", mesa.Estado);
        Assert.Equal(reserva.Id, mesa.ReservaId);
        Assert.Equal("Cliente Ejemplo", mesa.ClienteNombre);
        Assert.Equal(3, mesa.Personas);
    }

    [Fact]
    public async Task Una_Zona_Inactiva_Marca_Todas_Sus_Mesas_Inactivas_Y_Muestra_Sus_Reservas()
    {
        using var e = new Entorno();
        var reserva = await e.ReservarAsync(e.Mesa6DeZonaInactiva, EstadoReserva.EnCurso);

        var mapa = await e.MapaAsync();

        var zona = mapa.Zonas.Single(z => z.ZonaId == e.Cerrada.Id);
        Assert.False(zona.Activa);
        Assert.Equal(2, zona.Mesas.Count);
        Assert.All(zona.Mesas, m => Assert.Equal("Inactiva", m.Estado));
        Assert.Equal(reserva.Id, Mesa(mapa, 6).ReservaId); // aunque esté EnCurso, el estado sigue siendo Inactiva
        Assert.Null(Mesa(mapa, 5).ReservaId);
    }

    // --- Desempate (D) ---

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Con_Mas_De_Una_Reserva_Vigente_En_La_Misma_Mesa_Gana_EnCurso_En_Cualquier_Orden(bool enCursoPrimero)
    {
        using var e = new Entorno();
        Reserva enCurso, confirmada;
        if (enCursoPrimero)
        {
            enCurso = await e.ReservarAsync(e.Mesa1, EstadoReserva.EnCurso);
            confirmada = await e.ReservarAsync(e.Mesa1);
        }
        else
        {
            confirmada = await e.ReservarAsync(e.Mesa1);
            enCurso = await e.ReservarAsync(e.Mesa1, EstadoReserva.EnCurso);
        }

        var mesa = Mesa(await e.MapaAsync(), 1);

        Assert.Equal("Ocupada", mesa.Estado);
        Assert.Equal(enCurso.Id, mesa.ReservaId);
        Assert.NotEqual(confirmada.Id, mesa.ReservaId);
    }

    [Fact]
    public async Task Con_Dos_Reservas_Del_Mismo_Estado_El_Resultado_Es_Determinista()
    {
        using var e = new Entorno();
        var a = await e.ReservarAsync(e.Mesa1);
        var b = await e.ReservarAsync(e.Mesa1);

        var primero = Mesa(await e.MapaAsync(), 1).ReservaId;
        var segundo = Mesa(await e.MapaAsync(), 1).ReservaId;

        Assert.Equal(primero, segundo);
        Assert.Equal(new[] { a.Id, b.Id }.Min(), primero);
    }

    // --- Resumen, orden y estructura ---

    [Fact]
    public async Task El_Resumen_Cuenta_Cada_Estado_Y_Suma_El_Total_De_Mesas()
    {
        using var e = new Entorno();
        await e.ReservarAsync(e.Mesa1);                                  // Reservada
        await e.ReservarAsync(e.Mesa2, EstadoReserva.EnCurso);           // Ocupada
        await e.ReservarAsync(e.Mesa3InactivaPropia);                    // Inactiva (con reserva)

        var mapa = await e.MapaAsync();

        Assert.Equal(1, mapa.Resumen.Reservadas);
        Assert.Equal(1, mapa.Resumen.Ocupadas);
        Assert.Equal(1, mapa.Resumen.Libres); // la mesa 4
        Assert.Equal(3, mapa.Resumen.Inactivas); // 3, 5 y 6
        var total = mapa.Zonas.Sum(z => z.Mesas.Count);
        Assert.Equal(6, total);
        Assert.Equal(total, mapa.Resumen.Libres + mapa.Resumen.Reservadas + mapa.Resumen.Ocupadas + mapa.Resumen.Inactivas);
    }

    [Fact]
    public async Task Las_Zonas_Salen_Por_Nombre_Y_Las_Mesas_Por_Numero_Con_Los_Datos_De_La_Franja()
    {
        using var e = new Entorno();

        var mapa = await e.MapaAsync(franja: e.Cena);

        Assert.Equal(["Cerrada", "Salón", "Terraza"], mapa.Zonas.Select(z => z.Nombre).ToArray());
        Assert.Equal([1, 2, 3], mapa.Zonas.Single(z => z.Nombre == "Salón").Mesas.Select(m => m.Numero).ToArray());
        Assert.Equal(Hoy, mapa.Fecha);
        Assert.Equal(e.Cena.Id, mapa.FranjaId);
        Assert.Equal("Cena", mapa.FranjaNombre);
        Assert.Equal(new TimeOnly(18, 0), mapa.HoraInicio);
        Assert.Equal(new TimeOnly(22, 30), mapa.HoraFin);
    }

    [Fact]
    public async Task Una_Mesa_Borrada_Logicamente_No_Aparece()
    {
        using var e = new Entorno();
        e.Mesa1.IsDeleted = true;
        await e.Contexto.SaveChangesAsync();

        var mapa = await e.MapaAsync();

        Assert.DoesNotContain(mapa.Zonas.SelectMany(z => z.Mesas), m => m.Numero == 1);
        Assert.Equal(5, mapa.Zonas.Sum(z => z.Mesas.Count));
    }

    [Fact]
    public async Task Sin_Mesas_Devuelve_Un_Mapa_Vacio_Con_Resumen_En_Cero()
    {
        using var e = new Entorno();
        e.Contexto.Mesas.RemoveRange(e.Contexto.Mesas);
        await e.Contexto.SaveChangesAsync();

        var mapa = await e.MapaAsync();

        Assert.All(mapa.Zonas, z => Assert.Empty(z.Mesas));
        Assert.Equal(new ResumenOcupacionDto(0, 0, 0, 0), mapa.Resumen);
    }

    // --- Parámetros ---

    [Fact]
    public async Task Sin_Fecha_Usa_Hoy_En_Hora_De_Santo_Domingo_Y_Cualquier_Fecha_Valida_Se_Acepta()
    {
        using var e = new Entorno();
        await e.ReservarAsync(e.Mesa1);

        var sinFecha = await e.Servicio.ObtenerMapaAsync(new ConsultarOcupacionRequest { FranjaId = e.Almuerzo.Id.ToString() }, default);
        var pasada = await e.Servicio.ObtenerMapaAsync(
            new ConsultarOcupacionRequest { Fecha = "2020-01-15", FranjaId = e.Almuerzo.Id.ToString() }, default);
        var futura = await e.Servicio.ObtenerMapaAsync(
            new ConsultarOcupacionRequest { Fecha = "2031-06-30", FranjaId = e.Almuerzo.Id.ToString() }, default);

        Assert.Equal(Hoy, sinFecha.Value!.Fecha);
        Assert.Equal("Reservada", Mesa(sinFecha.Value, 1).Estado);
        Assert.Equal(new DateOnly(2020, 1, 15), pasada.Value!.Fecha);
        Assert.Equal(new DateOnly(2031, 6, 30), futura.Value!.Fecha);
    }

    [Theory]
    [InlineData(null, "La franja es obligatoria.")]
    [InlineData("", "La franja es obligatoria.")]
    [InlineData("   ", "La franja es obligatoria.")]
    [InlineData("no-es-un-guid", "La franja indicada no es válida.")]
    [InlineData("00000000-0000-0000-0000-000000000000", "La franja indicada no es válida.")]
    public async Task Una_Franja_Faltante_O_Invalida_Es_Validation_En_Espanol(string? franja, string mensaje)
    {
        using var e = new Entorno();

        var resultado = await e.Servicio.ObtenerMapaAsync(new ConsultarOcupacionRequest { FranjaId = franja }, default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
        Assert.Equal(mensaje, resultado.Error);
    }

    [Theory]
    [InlineData("01/12/2026")]
    [InlineData("2026-13-45")]
    [InlineData("mañana")]
    public async Task Una_Fecha_Invalida_Es_Validation_En_Espanol(string fecha)
    {
        using var e = new Entorno();

        var resultado = await e.Servicio.ObtenerMapaAsync(
            new ConsultarOcupacionRequest { Fecha = fecha, FranjaId = e.Almuerzo.Id.ToString() }, default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
        Assert.Equal("La fecha no es válida; usa el formato aaaa-mm-dd.", resultado.Error);
    }

    [Fact]
    public async Task Una_Franja_Inexistente_Es_NotFound_Y_Una_Inactiva_Se_Muestra()
    {
        using var e = new Entorno();
        e.Cena.Activa = false;
        await e.Contexto.SaveChangesAsync();

        var inexistente = await e.Servicio.ObtenerMapaAsync(new ConsultarOcupacionRequest { FranjaId = Guid.NewGuid().ToString() }, default);
        var inactiva = await e.MapaAsync(franja: e.Cena);

        Assert.Equal(ErrorType.NotFound, inexistente.Type);
        Assert.Equal("No encontramos esa franja.", inexistente.Error);
        Assert.Equal("Cena", inactiva.FranjaNombre);
    }

    // --- Datos expuestos y solo lectura ---

    [Fact]
    public async Task El_Estado_Sale_Como_Texto_Y_El_Mapa_No_Expone_El_Telefono()
    {
        using var e = new Entorno();
        await e.ReservarAsync(e.Mesa1);
        await e.ReservarAsync(e.Mesa2, EstadoReserva.EnCurso);

        var json = JsonSerializer.Serialize(await e.MapaAsync(), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"estado\":\"Reservada\"", json);
        Assert.Contains("\"estado\":\"Ocupada\"", json);
        Assert.Contains("\"estado\":\"Libre\"", json);
        Assert.Contains("\"estado\":\"Inactiva\"", json);
        Assert.DoesNotContain("8095550123", json);
        Assert.DoesNotContain("elefono", json);
        Assert.DoesNotContain(
            new[] { typeof(MapaOcupacionDto), typeof(ZonaOcupacionDto), typeof(MesaOcupacionDto), typeof(ResumenOcupacionDto) }
                .SelectMany(t => t.GetProperties()),
            p => p.Name.Contains("Telefono"));
    }

    [Fact]
    public async Task Las_Constantes_De_Estado_Son_Las_Cuatro_Del_Contrato()
    {
        await Task.CompletedTask;

        Assert.Equal("Libre", EstadoMesaOcupacion.Libre);
        Assert.Equal("Reservada", EstadoMesaOcupacion.Reservada);
        Assert.Equal("Ocupada", EstadoMesaOcupacion.Ocupada);
        Assert.Equal("Inactiva", EstadoMesaOcupacion.Inactiva);
    }

    [Fact]
    public async Task El_Repositorio_Es_Solo_Lectura_No_Deja_Entidades_En_El_Seguimiento_De_Cambios()
    {
        using var e = new Entorno();
        await e.ReservarAsync(e.Mesa1);
        e.Contexto.ChangeTracker.Clear();
        var repositorio = new OcupacionRepository(e.Contexto);

        await repositorio.ObtenerFranjaAsync(e.Almuerzo.Id, default);
        await repositorio.ListarZonasAsync(default);
        await repositorio.ListarMesasAsync(default);
        var reservas = await repositorio.ListarReservasVigentesAsync(Hoy, e.Almuerzo.Id, default);

        Assert.Single(reservas);
        Assert.Empty(e.Contexto.ChangeTracker.Entries());
    }
}
