using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Analitica;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Analitica;

public sealed class ModelosAfluenciaEndpointsTests
{
    [Fact]
    public async Task SolicitudDeEntrenamiento_Devuelve202YPermiteConsultarEstado()
    {
        await using var fabrica = new GiraApiFactory();
        using var cliente = fabrica.CreateClient();
        using (var scope = fabrica.Services.CreateScope())
        {
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            cliente.UsarToken(tokens.GenerarAccessToken(Guid.NewGuid(), "gerente@gira.local",
                "Gerente", [Roles.Gerente]).Token);
        }

        var respuesta = await cliente.PostAsync("/api/analitica/modelos/entrenamientos", content: null);
        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        var solicitado = await respuesta.Content.ReadFromJsonAsync<EntrenamientoModeloSolicitadoDto>();
        Assert.NotNull(solicitado);
        Assert.Equal("EnCola", solicitado.Estado);

        var estado = await cliente.GetFromJsonAsync<EntrenamientoModeloDto>(
            $"/api/analitica/modelos/entrenamientos/{solicitado.Id}");
        Assert.NotNull(estado);
        Assert.Equal(solicitado.Id, estado.Id);
        Assert.Equal("EnCola", estado.Estado);
    }

    [Fact]
    public async Task SolicitudDeEntrenamiento_RechazaUsuarioSinPermiso()
    {
        await using var fabrica = new GiraApiFactory();
        using var cliente = fabrica.CreateClient();
        using (var scope = fabrica.Services.CreateScope())
        {
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            cliente.UsarToken(tokens.GenerarAccessToken(Guid.NewGuid(), "supervisor@gira.local",
                "Supervisor", [Roles.Supervisor]).Token);
        }

        var respuesta = await cliente.PostAsync("/api/analitica/modelos/entrenamientos", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task ModelosYRollback_ExigenPermisoMotivoYAuditanLaVersionReactivada()
    {
        await using var fabrica = new GiraApiFactory();
        using var cliente = fabrica.CreateClient();
        var trabajoId = Guid.NewGuid();
        var franjaId = Guid.NewGuid();
        var anteriorId = Guid.NewGuid();
        var vigenteId = Guid.NewGuid();
        var solicitadoPor = Guid.NewGuid();
        var fecha = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
        using (var scope = fabrica.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
            db.FranjasHorarias.Add(new FranjaHoraria { Id = franjaId, Nombre = "Almuerzo" });
            db.Set<EntrenamientoModelo>().Add(new EntrenamientoModelo
            {
                Id = trabajoId,
                SolicitadoPor = solicitadoPor,
                Estado = EstadoEntrenamientoModelo.Completado,
                SolicitadoEn = fecha.AddDays(-2),
                FinalizadoEn = fecha.AddDays(-1)
            });
            db.Set<ModeloAfluencia>().AddRange(
                CrearModelo(anteriorId, trabajoId, franjaId, "ssa-anterior", fecha.AddDays(-10), false),
                CrearModelo(vigenteId, trabajoId, franjaId, "ssa-vigente", fecha.AddDays(-1), true));
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await cliente.GetAsync("/api/analitica/modelos")).StatusCode);
        Como(cliente, fabrica, Roles.Supervisor);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cliente.GetAsync("/api/analitica/modelos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync(
            $"/api/analitica/modelos/{anteriorId}/activar", new ActivarModeloRequest("Prueba"))).StatusCode);

        var gerenteId = Como(cliente, fabrica, Roles.Gerente);
        var modelos = await cliente.GetFromJsonAsync<List<ModeloAfluenciaDto>>("/api/analitica/modelos");
        Assert.Equal(2, modelos!.Count);
        Assert.Single(modelos, modelo => modelo.Activo);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync(
            $"/api/analitica/modelos/{anteriorId}/activar", new ActivarModeloRequest(" "))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PostAsJsonAsync(
            $"/api/analitica/modelos/{anteriorId}/activar",
            new ActivarModeloRequest("Regresión observada en el modelo vigente."))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.PostAsJsonAsync(
            $"/api/analitica/modelos/{vigenteId}/activar",
            new ActivarModeloRequest("No es una versión anterior."))).StatusCode);

        using var verificacion = fabrica.Services.CreateScope();
        var contexto = verificacion.ServiceProvider.GetRequiredService<GiraDbContext>();
        Assert.True((await contexto.Set<ModeloAfluencia>().SingleAsync(modelo => modelo.Id == anteriorId)).Activo);
        Assert.False((await contexto.Set<ModeloAfluencia>().SingleAsync(modelo => modelo.Id == vigenteId)).Activo);
        var auditoria = await contexto.Set<AuditoriaActivacionModelo>().SingleAsync();
        Assert.Equal(anteriorId, auditoria.ModeloActivadoId);
        Assert.Equal(vigenteId, auditoria.ModeloAnteriorId);
        Assert.Equal(gerenteId, auditoria.UsuarioId);
        Assert.True(auditoria.EsRollback);
        Assert.Equal("Regresión observada en el modelo vigente.", auditoria.Motivo);
    }

    private static ModeloAfluencia CrearModelo(
        Guid id, Guid entrenamientoId, Guid franjaId, string version, DateTimeOffset creadoEn, bool activo) => new()
    {
        Id = id,
        EntrenamientoId = entrenamientoId,
        FranjaHorariaId = franjaId,
        Version = version,
        EntrenamientoDesde = DateOnly.FromDateTime(creadoEn.UtcDateTime).AddDays(-70),
        EntrenamientoHasta = DateOnly.FromDateTime(creadoEn.UtcDateTime).AddDays(-15),
        ValidacionDesde = DateOnly.FromDateTime(creadoEn.UtcDateTime).AddDays(-14),
        ValidacionHasta = DateOnly.FromDateTime(creadoEn.UtcDateTime).AddDays(-1),
        Mae = 1,
        Rmse = 1,
        ArchivoModelo = [1],
        CreadoEn = creadoEn,
        Activo = activo
    };

    private static Guid Como(HttpClient cliente, GiraApiFactory fabrica, string rol)
    {
        var usuarioId = Guid.NewGuid();
        using var scope = fabrica.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        cliente.UsarToken(tokens.GenerarAccessToken(usuarioId, "gerente@gira.local", "Gerente", [rol]).Token);
        return usuarioId;
    }
}
