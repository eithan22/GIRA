using FluentValidation;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Tests.Common;

namespace GIRA.Tests.Reservas;

/// <summary>
/// La consulta de disponibilidad sin fecha (que llega como 0001-01-01) se rechaza con un mensaje propio, para el cliente
/// y para el personal, en vez de tratarse como una fecha pasada. Una fecha válida, pasada o futura, no cambia.
/// </summary>
public class ConsultarDisponibilidadValidatorTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);

    private static ConsultarDisponibilidadRequestValidator Crear() =>
        new(new ParametrosReservaFalso(), new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero)));

    private static Task<FluentValidation.Results.ValidationResult> Cliente(DateOnly fecha, int personas = 2) =>
        Crear().ValidateAsync(new ConsultarDisponibilidadRequest(fecha, personas));

    private static Task<FluentValidation.Results.ValidationResult> Personal(DateOnly fecha, int personas = 2) =>
        Crear().ValidateAsync(new ConsultarDisponibilidadRequest(fecha, personas), o => o.IncludeAllRuleSets());

    [Fact]
    public async Task SinFecha_Cliente_EsInvalida_ConElMensajeDeFechaObligatoria()
    {
        var resultado = await Cliente(default);

        Assert.False(resultado.IsValid);
        Assert.Equal(["La fecha es obligatoria."], resultado.Errors.Select(e => e.ErrorMessage).ToArray());
    }

    [Fact]
    public async Task SinFecha_Personal_SoloDiceQueLaFechaEsObligatoria_YNoQueEsDelPasado()
    {
        var resultado = await Personal(default);

        Assert.False(resultado.IsValid);
        Assert.Equal(["La fecha es obligatoria."], resultado.Errors.Select(e => e.ErrorMessage).ToArray());
    }

    [Fact]
    public async Task FechaDeHoyOFutura_EsValida_ParaElClienteYParaElPersonal()
    {
        Assert.True((await Cliente(Hoy)).IsValid);
        Assert.True((await Cliente(Hoy.AddDays(5))).IsValid);
        Assert.True((await Personal(Hoy)).IsValid);
        Assert.True((await Personal(Hoy.AddDays(5))).IsValid);
    }

    [Fact]
    public async Task FechaPasadaValida_SigueSiendoValidaParaElCliente_YElPersonalSigueConSu400()
    {
        Assert.True((await Cliente(Hoy.AddDays(-1))).IsValid);

        var personal = await Personal(Hoy.AddDays(-1));
        Assert.False(personal.IsValid);
        Assert.Equal(["La fecha no puede ser en el pasado."], personal.Errors.Select(e => e.ErrorMessage).ToArray());
    }

    [Fact]
    public async Task FechaFueraDeLaVentana_SigueIgual()
    {
        Assert.True((await Cliente(Hoy.AddDays(45))).IsValid);

        var personal = await Personal(Hoy.AddDays(45));
        Assert.Equal(["La fecha supera la ventana máxima de anticipación."], personal.Errors.Select(e => e.ErrorMessage).ToArray());
    }

    [Fact]
    public async Task SinFechaYSinPersonas_ReportaLosDosErrores()
    {
        var resultado = await Cliente(default, personas: 0);

        Assert.Equal(
            ["La fecha es obligatoria.", "Debe haber al menos 1 persona."],
            resultado.Errors.Select(e => e.ErrorMessage).ToArray());
    }
}
