using GIRA.Application.Services.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Tests.Common;

namespace GIRA.Tests.Personal;

public sealed class MarcacionTurnoServiceTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private static readonly DateTimeOffset Ahora = new(2026, 11, 18, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MarcarEntrada_CreaElTurnoDelUsuarioAutenticado()
    {
        var repositorio = new MarcacionTurnoRepositoryFalso();
        var servicio = new MarcacionTurnoService(repositorio, new TimeProviderFalso(Ahora));

        var resultado = await servicio.MarcarEntradaAsync(UsuarioId, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(UsuarioId, repositorio.Abierta!.UsuarioId);
        Assert.Equal(Ahora, resultado.Value!.InicioEn);
        Assert.Equal(1, repositorio.Guardados);
    }

    [Fact]
    public async Task NoPermiteDosTurnosAbiertosParaElMismoUsuario()
    {
        var repositorio = new MarcacionTurnoRepositoryFalso
        {
            Abierta = new MarcacionTurno { Id = Guid.NewGuid(), UsuarioId = UsuarioId, InicioEn = Ahora.AddHours(-1) }
        };
        var servicio = new MarcacionTurnoService(repositorio, new TimeProviderFalso(Ahora));

        var resultado = await servicio.MarcarEntradaAsync(UsuarioId, default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(0, repositorio.Guardados);
    }

    [Fact]
    public async Task MarcarSalida_DescuentaPausaDelTotalDeHoras()
    {
        var repositorio = new MarcacionTurnoRepositoryFalso
        {
            Abierta = new MarcacionTurno
            {
                Id = Guid.NewGuid(), UsuarioId = UsuarioId, InicioEn = Ahora.AddHours(-8)
            }
        };
        var servicio = new MarcacionTurnoService(repositorio, new TimeProviderFalso(Ahora));

        var resultado = await servicio.MarcarSalidaAsync(UsuarioId, 30, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(7.5m, resultado.Value!.HorasTrabajadas);
        Assert.Equal(Ahora, repositorio.Abierta.FinEn);
        Assert.Equal(30, repositorio.Abierta.MinutosPausa);
    }

    [Fact]
    public async Task RechazaPausaMayorQueDuracionDelTurnoSinCerrarElTurno()
    {
        var repositorio = new MarcacionTurnoRepositoryFalso
        {
            Abierta = new MarcacionTurno
            {
                Id = Guid.NewGuid(), UsuarioId = UsuarioId, InicioEn = Ahora.AddMinutes(-60)
            }
        };
        var servicio = new MarcacionTurnoService(repositorio, new TimeProviderFalso(Ahora));

        var resultado = await servicio.MarcarSalidaAsync(UsuarioId, 61, default);

        Assert.True(resultado.IsFailure);
        Assert.Null(repositorio.Abierta.FinEn);
        Assert.Equal(0, repositorio.Guardados);
    }

    private sealed class MarcacionTurnoRepositoryFalso : GIRA.Application.Interfaces.Repository.Personal.IMarcacionTurnoRepository
    {
        public MarcacionTurno? Abierta { get; set; }
        public int Guardados { get; private set; }

        public Task<MarcacionTurno?> ObtenerAbiertaAsync(Guid usuarioId, CancellationToken cancellationToken) =>
            Task.FromResult(Abierta?.UsuarioId == usuarioId ? Abierta : null);

        public Task AgregarAsync(MarcacionTurno marcacion, CancellationToken cancellationToken)
        {
            Abierta = marcacion;
            return Task.CompletedTask;
        }

        public Task GuardarAsync(CancellationToken cancellationToken)
        {
            Guardados++;
            return Task.CompletedTask;
        }
    }
}
