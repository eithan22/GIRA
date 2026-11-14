using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Services.Reservas;
using GIRA.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;

namespace GIRA.Tests.Reservas;

/// <summary>
/// El servicio de reservas y la disponibilidad leen los parámetros de la configuración (GI-226): máximo de
/// reservas vigentes por teléfono, tamaño máximo de grupo y día cerrado ya no están escritos en el código.
/// </summary>
public class ParametrosConfigurablesServicioTests
{
    private static readonly DateOnly Fecha = new(2026, 12, 25); // viernes
    private static readonly FranjaHoraria Almuerzo = new()
    {
        Id = Guid.NewGuid(), Nombre = "Almuerzo", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
    };
    private static readonly Mesa MesaGrande = new() { Id = Guid.NewGuid(), Numero = 1, Capacidad = 8 };

    private static TimeProviderFalso Reloj() =>
        new(new DateTimeOffset(Fecha.AddDays(-1).ToDateTime(new TimeOnly(8, 0)).AddHours(4), TimeSpan.Zero));

    private static ReservaService CrearServicio(ParametrosReservaFalso parametros, ReservaRepositoryFake? reservas = null)
    {
        var reloj = Reloj();
        var publisher = new PublisherFalso();
        return new ReservaService(
            new MesaRepositoryFalso([MesaGrande]),
            new FranjaHorariaRepositoryFalso([Almuerzo]),
            reservas ?? new ReservaRepositoryFake(),
            new ClienteRepositoryFake(),
            new MesaGestionRepositoryFalso(),
            parametros,
            new CrearReservaRequestValidator(parametros, reloj),
            new ListarReservasRequestValidator(),
            new CheckInRequestValidator(),
            new CancelarPorPersonalRequestValidator(),
            new ModificarReservaRequestValidator(),
            new GeneradorCodigoReserva(),
            publisher,
            new PublicadorEventosSeguro(publisher, NullLogger<PublicadorEventosSeguro>.Instance),
            reloj);
    }

    private static CrearReservaRequest Request(int personas = 2) =>
        new("Ana Pérez", "8095551234", Fecha, Almuerzo.Id, personas);

    // --- Máximo de reservas vigentes por teléfono ---

    [Fact]
    public async Task Con_Maximo_3_Un_Teléfono_Con_2_Vigentes_Todavia_Puede_Reservar()
    {
        var parametros = new ParametrosReservaFalso { MaxReservasVigentesPorTelefono = 3 };

        var resultado = await CrearServicio(parametros, new ReservaRepositoryFake { VigentesPorTelefono = 2 })
            .CrearAsync(Request(), default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Con_Maximo_3_Un_Teléfono_Con_3_Vigentes_Es_Conflict_Con_El_Valor_Real_En_El_Mensaje()
    {
        var parametros = new ParametrosReservaFalso { MaxReservasVigentesPorTelefono = 3 };

        var web = await CrearServicio(parametros, new ReservaRepositoryFake { VigentesPorTelefono = 3 })
            .CrearAsync(Request(), default);
        var personal = await CrearServicio(parametros, new ReservaRepositoryFake { VigentesPorTelefono = 3 })
            .CrearComoPersonalAsync(Request(), OrigenReserva.Telefono, default);

        Assert.Equal(ErrorType.Conflict, web.Type);
        Assert.Equal("Ya tienes 3 reservas vigentes con este teléfono; cancela una para reservar otra.", web.Error);
        Assert.Equal("Este teléfono ya tiene 3 reservas vigentes; cancela una para registrar otra.", personal.Error);
    }

    [Fact]
    public async Task Con_Maximo_1_El_Mensaje_Va_En_Singular()
    {
        var parametros = new ParametrosReservaFalso { MaxReservasVigentesPorTelefono = 1 };

        var resultado = await CrearServicio(parametros, new ReservaRepositoryFake { VigentesPorTelefono = 1 })
            .CrearAsync(Request(), default);

        Assert.Equal("Ya tienes 1 reserva vigente con este teléfono; cancela una para reservar otra.", resultado.Error);
    }

    // --- Tamaño máximo de grupo: min(configurado, mesa activa más grande) ---

    [Fact]
    public async Task Maximo_Configurado_Menor_Que_La_Mesa_Manda_Y_El_Mensaje_Lleva_El_Maximo_Configurado()
    {
        var parametros = new ParametrosReservaFalso { MaxPersonasPorReserva = 4 };

        var cinco = await CrearServicio(parametros).CrearAsync(Request(personas: 5), default);
        var cuatro = await CrearServicio(parametros).CrearAsync(Request(personas: 4), default);

        Assert.Equal(ErrorType.Validation, cinco.Type);
        Assert.Equal("Para grupos de más de 4 personas, llama al restaurante.", cinco.Error);
        Assert.True(cuatro.IsSuccess);
    }

    [Fact]
    public async Task Maximo_Configurado_Mayor_Que_La_Mesa_Manda_La_Mesa()
    {
        var parametros = new ParametrosReservaFalso { MaxPersonasPorReserva = 20 };

        var resultado = await CrearServicio(parametros).CrearComoPersonalAsync(Request(personas: 9), OrigenReserva.Telefono, default);

        Assert.Equal(
            "Las mesas admiten como máximo 8 personas. Los grupos más grandes se atienden por teléfono, fuera del sistema.",
            resultado.Error);
    }

    // --- Día cerrado: bloquea al cliente y al personal ---

    [Fact]
    public async Task Fecha_Cerrada_Bloquea_Al_Cliente_Y_Al_Personal_Con_El_Mismo_Mensaje()
    {
        var parametros = new ParametrosReservaFalso { FechasCerradas = new HashSet<DateOnly> { Fecha } };

        var web = await CrearServicio(parametros).CrearAsync(Request(), default);
        var personal = await CrearServicio(parametros).CrearComoPersonalAsync(Request(), OrigenReserva.Presencial, default);

        Assert.Equal(ErrorType.Validation, web.Type);
        Assert.Equal("El restaurante está cerrado ese día.", web.Error);
        Assert.Equal(ErrorType.Validation, personal.Type);
        Assert.Equal("El restaurante está cerrado ese día.", personal.Error);
    }

    [Fact]
    public async Task Dia_De_La_Semana_Cerrado_Tambien_Bloquea_Al_Personal()
    {
        var parametros = new ParametrosReservaFalso { DiasCerrados = new HashSet<DayOfWeek> { Fecha.DayOfWeek } };

        var personal = await CrearServicio(parametros).CrearComoPersonalAsync(Request(), OrigenReserva.Telefono, default);

        Assert.Equal("El restaurante está cerrado ese día.", personal.Error);
    }

    // --- Antelación máxima configurable en el validador asíncrono ---

    [Fact]
    public async Task Antelacion_Maxima_Configurada_Se_Aplica_Al_Crear()
    {
        var parametros = new ParametrosReservaFalso { AntelacionMaxima = TimeSpan.Zero };

        var resultado = await CrearServicio(parametros).CrearAsync(Request(), default); // la fecha es mañana: pasa de 0 días

        Assert.Equal(ErrorType.Validation, resultado.Type);
        Assert.Contains("La fecha supera la ventana máxima de anticipación.", resultado.Error);
    }
}
