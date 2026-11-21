using System.Collections.Concurrent;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Events.Inventario;
using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Inventario;

/// <summary>Atajos de las pruebas HTTP de Inventario: tokens, altas mínimas, movimientos y eventos.</summary>
internal static class ApiInventario
{
    /// <summary>Token firmado por la API para un usuario (que no tiene por qué existir) con ese rol.</summary>
    public static string Token(GiraApiFactory factory, string rol, Guid? usuarioId = null)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ITokenService>()
            .GenerarAccessToken(usuarioId ?? Guid.NewGuid(), "personal@gira.local", "Personal de Prueba", [rol]).Token;
    }

    /// <summary>Token de un usuario real ("Ana Pérez") para que el kardex y las alertas muestren su nombre.</summary>
    public static async Task<(string Token, Guid UsuarioId)> TokenDeUsuarioRealAsync(GiraApiFactory factory, string rol)
    {
        var id = await factory.CrearUsuarioAsync($"{Guid.NewGuid():N}@gira.local", "Clave#Segura1", true, rol);
        return (Token(factory, rol, id), id);
    }

    /// <summary>Publica un evento como lo haría otro módulo.</summary>
    public static async Task PublicarAsync(GiraApiFactory factory, INotification evento)
    {
        using var scope = factory.Services.CreateScope();
        if (evento is VisitaCompletadaEvent visita)
        {
            var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
            if (!await db.FranjasHorarias.AnyAsync(franja => franja.Id == visita.FranjaHorariaId))
            {
                db.FranjasHorarias.Add(new FranjaHoraria
                {
                    Id = visita.FranjaHorariaId,
                    Nombre = "Franja de prueba"
                });
                await db.SaveChangesAsync();
            }
        }
        await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(evento);
    }

    /// <summary>POST /api/movimientos.</summary>
    public static Task<HttpResponseMessage> Movimiento(
        HttpClient cliente, Guid insumoId, string tipo, decimal cantidad, string? motivo = null,
        DateOnly? vence = null, decimal? costoUnitario = null) =>
        cliente.PostAsJsonAsync("/api/movimientos",
            new RegistrarMovimientoRequest(insumoId, tipo, cantidad, costoUnitario, motivo, null, vence));

    /// <summary>Crea un insumo (y la unidad y la categoría si todavía no hay).</summary>
    public static async Task<InsumoDto> CrearInsumoAsync(
        HttpClient cliente, string nombre, decimal reorden, bool perecedero = false, Guid? proveedorId = null) =>
        await LeerAsync<InsumoDto>(await cliente.PostAsJsonAsync("/api/insumos", await InsumoRequestAsync(cliente, nombre, reorden, perecedero, proveedorId)));

    /// <summary>Cuerpo válido para crear un insumo, con la primera unidad y categoría (las crea si no hay).</summary>
    public static async Task<GuardarInsumoRequest> InsumoRequestAsync(
        HttpClient cliente, string nombre, decimal reorden, bool perecedero = false, Guid? proveedorId = null)
    {
        var unidades = await cliente.GetFromJsonAsync<List<UnidadMedidaDto>>("/api/unidades-medida");
        var unidad = unidades!.FirstOrDefault() ?? await LeerAsync<UnidadMedidaDto>(
            await cliente.PostAsJsonAsync("/api/unidades-medida", new GuardarUnidadMedidaRequest("Kilogramo", "kg")));
        var categorias = await cliente.GetFromJsonAsync<List<CategoriaDto>>("/api/categorias-insumo");
        var categoria = categorias!.FirstOrDefault() ?? await LeerAsync<CategoriaDto>(
            await cliente.PostAsJsonAsync("/api/categorias-insumo", new GuardarCategoriaRequest("General")));
        return new GuardarInsumoRequest(nombre, categoria.Id, unidad.Id, reorden, 10, perecedero, proveedorId);
    }

    /// <summary>Crea un plato con su propia categoría.</summary>
    public static async Task<PlatoGuardadoDto> CrearPlatoAsync(HttpClient cliente, string nombre, decimal demanda, bool activo = true)
    {
        var categoria = await LeerAsync<CategoriaDto>(
            await cliente.PostAsJsonAsync("/api/categorias-plato", new GuardarCategoriaRequest($"Categoría de {nombre}")));
        return await LeerAsync<PlatoGuardadoDto>(
            await cliente.PostAsJsonAsync("/api/platos", new GuardarPlatoRequest(nombre, categoria.Id, 300, demanda, activo)));
    }

    /// <summary>PUT de la receta; falla la prueba si no responde 2xx.</summary>
    public static async Task GuardarRecetaAsync(HttpClient cliente, Guid platoId, int porciones, params (Guid InsumoId, decimal Cantidad)[] lineas) =>
        await LeerAsync<RecetaDto>(await cliente.PutAsJsonAsync($"/api/platos/{platoId}/receta",
            new GuardarRecetaRequest(porciones, null, lineas.Select(l => new LineaRecetaRequest(l.InsumoId, l.Cantidad)).ToList())));

    /// <summary>Stock actual del insumo.</summary>
    public static async Task<decimal> StockAsync(HttpClient cliente, Guid insumoId) =>
        (await cliente.GetFromJsonAsync<InsumoDto>($"/api/insumos/{insumoId}"))!.CantidadStock;

    /// <summary>Alertas en ese estado (todas las páginas que entran en 100).</summary>
    public static async Task<IReadOnlyList<AlertaInventarioDto>> AlertasAsync(HttpClient cliente, string estado = "Abierta") =>
        (await cliente.GetFromJsonAsync<GIRA.Application.Common.PagedResult<AlertaInventarioDto>>(
            $"/api/inventario/alertas?estado={estado}&pageSize=100"))!.Items;

    /// <summary>Lee el cuerpo; falla la prueba con el detalle del servidor si no es 2xx.</summary>
    public static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta)
    {
        Assert.True(respuesta.IsSuccessStatusCode, $"{(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
        return (await respuesta.Content.ReadFromJsonAsync<T>())!;
    }
}

/// <summary>Personas con reserva por fecha que la prueba puede cambiar entre un evento y otro.</summary>
internal sealed class ReservasFalsas : IConsultaReservasInventario
{
    /// <summary>Personas por fecha.</summary>
    public ConcurrentDictionary<DateOnly, int> Personas { get; } = new();

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<DateOnly, int>> PersonasConReservaPorFechaAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<DateOnly, int>>(Personas.Where(p => p.Key >= desde && p.Key <= hasta).ToDictionary());
}

/// <summary>Guarda cada <see cref="AlertaInventarioGeneradaEvent"/> publicado.</summary>
internal sealed class AlertasPublicadas : INotificationHandler<AlertaInventarioGeneradaEvent>
{
    /// <summary>Eventos recibidos (compartidos: el handler se registra como singleton).</summary>
    public ConcurrentQueue<AlertaInventarioGeneradaEvent> Eventos { get; } = new();

    /// <inheritdoc />
    public Task Handle(AlertaInventarioGeneradaEvent notification, CancellationToken cancellationToken)
    {
        Eventos.Enqueue(notification);
        return Task.CompletedTask;
    }
}
