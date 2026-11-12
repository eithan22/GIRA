using Microsoft.Extensions.Logging.Abstractions;
using GIRA.Infrastructure.Services.Reservas;
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
/// Pruebas de la agenda del personal (GI-164): listado con filtros, detalle y búsqueda de
/// clientes, con repositorios reales sobre EF Core InMemory (una base nueva por prueba).
/// </summary>
public class AgendaServiciosTests
{
    // "Hoy" local = 2026-12-01 (12:00 en República Dominicana, UTC-4).
    private static readonly DateOnly Hoy = new(2026, 12, 1);
    private static readonly DateOnly Manana = new(2026, 12, 2);

    private sealed class Entorno : IDisposable
    {
        public GiraDbContext Contexto { get; }
        public ReservaService Reservas { get; }
        public ClienteService Clientes { get; }
        public Zona Salon { get; private set; } = null!;
        public Zona Terraza { get; private set; } = null!;
        public FranjaHoraria Almuerzo { get; private set; } = null!;
        public FranjaHoraria Cena { get; private set; } = null!;
        public Mesa Mesa1 { get; private set; } = null!;
        public Mesa Mesa2 { get; private set; } = null!;
        public Mesa Mesa3 { get; private set; } = null!;

        public Entorno()
        {
            var opciones = new DbContextOptionsBuilder<GiraDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Contexto = new GiraDbContext(opciones);

            var reloj = new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero));
            var parametros = new ParametrosReservaFalso();
            var clientes = new ClienteRepository(Contexto);

            Reservas = new ReservaService(
                new MesaRepository(Contexto),
                new FranjaHorariaRepository(Contexto),
                new ReservaRepository(Contexto),
                clientes,
                new MesaGestionRepository(Contexto),
                parametros,
                new CrearReservaRequestValidator(parametros, reloj),
                new ListarReservasRequestValidator(),
                new CheckInRequestValidator(),
                new CancelarPorPersonalRequestValidator(),
                new ModificarReservaRequestValidator(),
                new GeneradorCodigoReserva(),
                new PublisherFalso(),
                new PublicadorEventosSeguro(new PublisherFalso(), NullLogger<PublicadorEventosSeguro>.Instance),
                reloj);
            Clientes = new ClienteService(clientes, new BuscarClientesRequestValidator());
        }

        public void Dispose() => Contexto.Dispose();

        // Salón (mesas 1 y 2) y Terraza (mesa 3); franjas Almuerzo 12:00-15:00 y Cena 18:00-22:30.
        public async Task SembrarSalonAsync()
        {
            Salon = new Zona { Id = Guid.NewGuid(), Nombre = "Salón" };
            Terraza = new Zona { Id = Guid.NewGuid(), Nombre = "Terraza" };
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
            Mesa3 = new Mesa { Id = Guid.NewGuid(), Numero = 3, Capacidad = 6, ZonaId = Terraza.Id };
            Contexto.Zonas.AddRange(Salon, Terraza);
            Contexto.FranjasHorarias.AddRange(Almuerzo, Cena);
            Contexto.Mesas.AddRange(Mesa1, Mesa2, Mesa3);
            await Contexto.SaveChangesAsync();
        }

        public async Task<Cliente> SembrarClienteAsync(string nombre, string telefono)
        {
            var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = nombre, Telefono = telefono };
            Contexto.Clientes.Add(cliente);
            await Contexto.SaveChangesAsync();
            return cliente;
        }

        public async Task<Reserva> SembrarReservaAsync(
            Cliente cliente, Mesa mesa, FranjaHoraria franja, DateOnly fecha,
            int personas = 2, EstadoReserva estado = EstadoReserva.Confirmada, string? motivo = null)
        {
            var reserva = new Reserva
            {
                Id = Guid.NewGuid(),
                Codigo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                ClienteId = cliente.Id,
                MesaId = mesa.Id,
                FranjaHorariaId = franja.Id,
                Fecha = fecha,
                Personas = personas,
                Origen = OrigenReserva.Telefono
            };
            switch (estado)
            {
                case EstadoReserva.EnCurso:
                    reserva.HacerCheckIn(personas + 1, new DateTime(2026, 12, 1, 16, 5, 0, DateTimeKind.Utc));
                    break;
                case EstadoReserva.Completada:
                    reserva.HacerCheckIn(personas, new DateTime(2026, 12, 1, 16, 5, 0, DateTimeKind.Utc));
                    reserva.Completar();
                    break;
                case EstadoReserva.Cancelada:
                    reserva.Cancelar(motivo);
                    break;
                case EstadoReserva.NoAsistio:
                    reserva.MarcarNoAsistio();
                    break;
            }
            Contexto.Reservas.Add(reserva);
            await Contexto.SaveChangesAsync();
            return reserva;
        }
    }

    private static void EsFallo<T>(OperationResult<T> resultado, ErrorType tipo)
    {
        Assert.False(resultado.IsSuccess);
        Assert.Equal(tipo, resultado.Type);
    }

    // --- Listado ---

    [Fact]
    public async Task Listar_Sin_Fecha_Ni_Telefono_Muestra_Solo_Hoy()
    {
        using var e = new Entorno();
        await e.SembrarSalonAsync();
        var ana = await e.SembrarClienteAsync("Ana Ejemplo", "8095550101");
        await e.SembrarReservaAsync(ana, e.Mesa1, e.Almuerzo, Hoy);
        await e.SembrarReservaAsync(ana, e.Mesa1, e.Almuerzo, Manana);

        var resultado = await e.Reservas.ListarAsync(new ListarReservasRequest(), default);

        Assert.True(resultado.IsSuccess);
        var item = Assert.Single(resultado.Value!.Items);
        Assert.Equal(Hoy, item.Fecha);
        Assert.Equal(1, resultado.Value.Total);
    }

    [Fact]
    public async Task Listar_Filtra_Por_Fecha()
    {
        using var e = new Entorno();
        await e.SembrarSalonAsync();
        var ana = await e.SembrarClienteAsync("Ana Ejemplo", "8095550101");
        await e.SembrarReservaAsync(ana, e.Mesa1, e.Almuerzo, Hoy);
        await e.SembrarReservaAsync(ana, e.Mesa2, e.Almuerzo, Manana);

        var resultado = await e.Reservas.ListarAsync(new ListarReservasRequest { Fecha = Manana }, default);

        var item = Assert.Single(resultado.Value!.Items);
        Assert.Equal(Manana, item.Fecha);
    }

    [Fact]
    public async Task Listar_Filtra_Por_Estado_Sin_Distinguir_Mayusculas()
    {
        using var e = new Entorno();
        await e.SembrarSalonAsync();
        var ana = await e.SembrarClienteAsync("Ana Ejemplo", "8095550101");
        await e.SembrarReservaAsync(ana, e.Mesa1, e.Almuerzo, Hoy);
        await e.SembrarReservaAsync(ana, e.Mesa2, e.Almuerzo, Hoy, estado: EstadoReserva.EnCurso);

        var resultado = await e.Reservas.ListarAsync(new ListarReservasRequest { Estado = "enCURSO" }, default);

        var item = Assert.Single(resultado.Value!.Items);
        Assert.Equal("EnCurso", item.Estado);
        Assert.Equal(3, item.PersonasReales);
    }

    [Fact]
    public async Task Listar_Filtra_Por_Franja()
    {
        using var e = new Entorno();
        await e.SembrarSalonAsync();
        var ana = await e.SembrarClienteAsync("Ana Ejemplo", "8095550101");
        await e.SembrarReservaAsync(ana, e.Mesa1, e.Almuerzo, Hoy);
        await e.SembrarReservaAsync(ana, e.Mesa1, e.Cena, Hoy);

        var resultado = await e.Reservas.ListarAsync(new ListarReservasRequest { FranjaId = e.Cena.Id }, default);

        var item = Assert.Single(resultado.Value!.Items);
        Assert.Equal("Cena", item.FranjaNombre);
    }

    [Fact]
    public async Task Listar_Por_Telefono_Sin_Fecha_Trae_Todas_Sus_Fechas_Y_Normaliza_El_Telefono()
    {
        using var e = new Entorno();
        await e.SembrarSalonAsync();
        var ana = await e.SembrarClienteAsync("Ana Ejemplo", "8095550101");
        var beto = await e.SembrarClienteAsync("Beto Ejemplo", "8295550202");
        await e.SembrarReservaAsync(ana, e.Mesa1, e.Almuerzo, Hoy);
        await e.SembrarReservaAsync(ana, e.Mesa1, e.Almuerzo, Manana);
        await e.SembrarReservaAsync(beto, e.Mesa2, e.Almuerzo, Hoy);

        var resultado = await e.Reservas.ListarAsync(new ListarReservasRequest { Telefono = "809-555 0101" }, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(2, resultado.Value!.Total);
        Assert.All(resultado.Value.Items, i => Assert.Equal("8095550101", i.ClienteTelefono));
    }

    [Fact]
    public async Task Listar_Ordena_Por_Hora_De_Franja_Y_Numero_De_Mesa()
    {
        using var e = new Entorno();
        await e.SembrarSalonAsync();
        var ana = await e.SembrarClienteAsync("Ana Ejemplo", "8095550101");
        await e.SembrarReservaAsync(ana, e.Mesa2, e.Cena, Hoy);
        await e.SembrarReservaAsync(ana, e.Mesa3, e.Almuerzo, Hoy);
        await e.SembrarReservaAsync(ana, e.Mesa1, e.Almuerzo, Hoy);

        var resultado = await e.Reservas.ListarAsync(new ListarReservasRequest(), default);

        var orden = resultado.Value!.Items.Select(i => (i.FranjaNombre, i.MesaNumero)).ToList();
        Assert.Equal([("Almuerzo", 1), ("Almuerzo", 3), ("Cena", 2)], orden);
    }

    [Fact]
    public async Task Listar_Pagina_Y_Devuelve_El_Total()
    {
        using var e = new Entorno();
        await e.SembrarSalonAsync();
        var ana = await e.SembrarClienteAsync("Ana Ejemplo", "8095550101");
        await e.SembrarReservaAsync(ana, e.Mesa1, e.Almuerzo, Hoy);
        await e.SembrarReservaAsync(ana, e.Mesa2, e.Almuerzo, Hoy);
        await e.SembrarReservaAsync(ana, e.Mesa3, e.Almuerzo, Hoy);

        var resultado = await e.Reservas.ListarAsync(new ListarReservasRequest { Page = 2, PageSize = 2 }, default);

        Assert.Equal(3, resultado.Value!.Total);
        Assert.Equal(2, resultado.Value.Page);
        var item = Assert.Single(resultado.Value.Items);
        Assert.Equal(3, item.MesaNumero);
    }

    [Fact]
    public async Task Listar_Devuelve_Cliente_Mesa_Zona_Y_Estado_Como_Texto()
    {
        using var e = new Entorno();
        await e.SembrarSalonAsync();
        var ana = await e.SembrarClienteAsync("Ana Ejemplo", "8095550101");
        await e.SembrarReservaAsync(ana, e.Mesa3, e.Cena, Hoy, personas: 4);

        var item = Assert.Single((await e.Reservas.ListarAsync(new ListarReservasRequest(), default)).Value!.Items);

        Assert.Equal("Confirmada", item.Estado);
        Assert.Equal("Telefono", item.Origen);
        Assert.Equal("Ana Ejemplo", item.ClienteNombre);
        Assert.Equal(3, item.MesaNumero);
        Assert.Equal("Terraza", item.ZonaNombre);
        Assert.Equal(new TimeOnly(18, 0), item.FranjaHoraInicio);
        Assert.Equal(4, item.Personas);
        Assert.Null(item.PersonasReales);
    }

    [Theory]
    [InlineData(0, 20, null, null)]
    [InlineData(1, 0, null, null)]
    [InlineData(1, 101, null, null)]
    [InlineData(1, 20, "Inexistente", null)]
    [InlineData(1, 20, "1", null)]
    [InlineData(1, 20, null, "12345")]
    [InlineData(1, 20, null, "+18095550101")]
    public async Task Listar_Con_Datos_Invalidos_Es_Validation(int page, int pageSize, string? estado, string? telefono)
    {
        using var e = new Entorno();

        var resultado = await e.Reservas.ListarAsync(
            new ListarReservasRequest { Page = page, PageSize = pageSize, Estado = estado, Telefono = telefono }, default);

        EsFallo(resultado, ErrorType.Validation);
    }

    // --- Detalle ---

    [Fact]
    public async Task Obtener_Devuelve_El_Detalle_Con_Motivo_De_Cancelacion()
    {
        using var e = new Entorno();
        await e.SembrarSalonAsync();
        var ana = await e.SembrarClienteAsync("Ana Ejemplo", "8095550101");
        var reserva = await e.SembrarReservaAsync(
            ana, e.Mesa2, e.Almuerzo, Hoy, estado: EstadoReserva.Cancelada, motivo: "Cliente avisó que no viene");

        var resultado = await e.Reservas.ObtenerAsync(reserva.Id, default);

        Assert.True(resultado.IsSuccess);
        var d = resultado.Value!;
        Assert.Equal("Cancelada", d.Estado);
        Assert.Equal("Cliente avisó que no viene", d.MotivoCancelacion);
        Assert.Equal(reserva.Codigo, d.Codigo);
        Assert.Equal(e.Mesa2.Id, d.MesaId);
        Assert.Equal(4, d.MesaCapacidad);
        Assert.Equal(ana.Id, d.ClienteId);
        Assert.Equal("Salón", d.ZonaNombre);
    }

    [Fact]
    public async Task Obtener_Con_Check_In_Devuelve_Personas_Reales_Y_Fecha_De_Check_In()
    {
        using var e = new Entorno();
        await e.SembrarSalonAsync();
        var ana = await e.SembrarClienteAsync("Ana Ejemplo", "8095550101");
        var reserva = await e.SembrarReservaAsync(ana, e.Mesa2, e.Almuerzo, Hoy, personas: 3, estado: EstadoReserva.EnCurso);

        var d = (await e.Reservas.ObtenerAsync(reserva.Id, default)).Value!;

        Assert.Equal("EnCurso", d.Estado);
        Assert.Equal(4, d.PersonasReales);
        Assert.NotNull(d.FechaCheckIn);
        Assert.Null(d.MotivoCancelacion);
    }

    [Fact]
    public async Task Obtener_Inexistente_Es_NotFound_Con_Mensaje_En_Espanol()
    {
        using var e = new Entorno();

        var resultado = await e.Reservas.ObtenerAsync(Guid.NewGuid(), default);

        EsFallo(resultado, ErrorType.NotFound);
        Assert.Equal("No encontramos esa reserva.", resultado.Error);
    }

    // --- Clientes ---

    [Fact]
    public async Task Buscar_Clientes_Por_Nombre_Sin_Distinguir_Mayusculas()
    {
        using var e = new Entorno();
        await e.SembrarClienteAsync("María Ejemplo", "8095550101");
        await e.SembrarClienteAsync("Pedro Ejemplo", "8295550202");

        var resultado = await e.Clientes.BuscarAsync(new BuscarClientesRequest { Buscar = "MARÍA" }, default);

        var cliente = Assert.Single(resultado.Value!.Items);
        Assert.Equal("María Ejemplo", cliente.Nombre);
    }

    [Fact]
    public async Task Buscar_Clientes_Por_Telefono_Parcial_Con_Guiones()
    {
        using var e = new Entorno();
        await e.SembrarClienteAsync("María Ejemplo", "8095550101");
        await e.SembrarClienteAsync("Pedro Ejemplo", "8295550202");

        var resultado = await e.Clientes.BuscarAsync(new BuscarClientesRequest { Buscar = "555-0202" }, default);

        var cliente = Assert.Single(resultado.Value!.Items);
        Assert.Equal("8295550202", cliente.Telefono);
    }

    [Fact]
    public async Task Buscar_Clientes_Pagina_Y_Ordena_Por_Nombre()
    {
        using var e = new Entorno();
        await e.SembrarClienteAsync("Cliente C", "8095550103");
        await e.SembrarClienteAsync("Cliente A", "8095550101");
        await e.SembrarClienteAsync("Cliente B", "8095550102");

        var resultado = await e.Clientes.BuscarAsync(new BuscarClientesRequest { Buscar = "cliente", Page = 2, PageSize = 2 }, default);

        Assert.Equal(3, resultado.Value!.Total);
        Assert.Equal("Cliente C", Assert.Single(resultado.Value.Items).Nombre);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ab ")]
    public async Task Buscar_Clientes_Con_Menos_De_3_Caracteres_Es_Validation(string? texto)
    {
        using var e = new Entorno();

        var resultado = await e.Clientes.BuscarAsync(new BuscarClientesRequest { Buscar = texto }, default);

        EsFallo(resultado, ErrorType.Validation);
        Assert.Equal("Escribe al menos 3 caracteres para buscar.", resultado.Error);
    }
}
