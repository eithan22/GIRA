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
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Tests.Reservas;

public class SalonServiciosTests
{
    // "Hoy" local = 2026-12-01 (12:00 en República Dominicana).
    private static readonly DateOnly Futuro = new(2026, 12, 5);
    private static readonly DateOnly Pasado = new(2026, 11, 20);

    private sealed class Entorno : IDisposable
    {
        public GiraDbContext Contexto { get; }
        public ZonaService Zonas { get; }
        public MesaService Mesas { get; }
        public FranjaService Franjas { get; }

        public Entorno()
        {
            var opciones = new DbContextOptionsBuilder<GiraDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Contexto = new GiraDbContext(opciones);

            var reloj = new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero));
            var zonas = new ZonaRepository(Contexto);
            var mesas = new MesaGestionRepository(Contexto);
            var franjas = new FranjaGestionRepository(Contexto);
            var futuras = new ReservasFuturasRepository(Contexto);
            var listado = new ListarSalonRequestValidator();

            Zonas = new ZonaService(zonas, futuras, new GuardarZonaRequestValidator(), listado, reloj);
            Mesas = new MesaService(mesas, zonas, futuras, new GuardarMesaRequestValidator(), listado, reloj);
            Franjas = new FranjaService(franjas, futuras, new ParametrosReservaFalso { HoraApertura = new TimeOnly(8, 0) }, new GuardarFranjaRequestValidator(), listado, reloj);
        }

        public void Dispose() => Contexto.Dispose();

        public async Task<Zona> SembrarZonaAsync(string nombre = "Salón", bool activa = true)
        {
            var zona = new Zona { Id = Guid.NewGuid(), Nombre = nombre, Activa = activa };
            Contexto.Zonas.Add(zona);
            await Contexto.SaveChangesAsync();
            return zona;
        }

        public async Task<Mesa> SembrarMesaAsync(Guid zonaId, int numero = 1, int capacidad = 4)
        {
            var mesa = new Mesa { Id = Guid.NewGuid(), Numero = numero, Capacidad = capacidad, ZonaId = zonaId };
            Contexto.Mesas.Add(mesa);
            await Contexto.SaveChangesAsync();
            return mesa;
        }

        public async Task<FranjaHoraria> SembrarFranjaAsync(string nombre, int horaInicio, int horaFin, bool activa = true)
        {
            var franja = new FranjaHoraria
            {
                Id = Guid.NewGuid(),
                Nombre = nombre,
                HoraInicio = new TimeOnly(horaInicio, 0),
                HoraFin = new TimeOnly(horaFin, 0),
                Activa = activa
            };
            Contexto.FranjasHorarias.Add(franja);
            await Contexto.SaveChangesAsync();
            return franja;
        }

        public async Task SembrarReservaAsync(
            Guid mesaId, Guid franjaId, DateOnly fecha, int personas = 2, bool cancelada = false)
        {
            var reserva = new Reserva
            {
                Codigo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                ClienteId = Guid.NewGuid(),
                MesaId = mesaId,
                FranjaHorariaId = franjaId,
                Fecha = fecha,
                Personas = personas,
                Origen = OrigenReserva.Presencial
            };
            if (cancelada)
            {
                reserva.Cancelar("Prueba");
            }

            Contexto.Reservas.Add(reserva);
            await Contexto.SaveChangesAsync();
        }
    }

    private static void EsFallo(OperationResult resultado, ErrorType tipo)
    {
        Assert.True(resultado.IsFailure, "Se esperaba un fallo.");
        Assert.Equal(tipo, resultado.Type!.Value);
    }

    // ---- Zonas (GI-193)

    [Fact]
    public async Task Zona_Crear_Guarda_Activa_Y_Sin_Espacios()
    {
        using var e = new Entorno();
        var resultado = await e.Zonas.CrearAsync(new GuardarZonaRequest("  Terraza  "), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Terraza", resultado.Value!.Nombre);
        Assert.True(resultado.Value.Activa);
    }

    [Fact]
    public async Task Zona_Crear_Nombre_Duplicado_Sin_Distinguir_Mayusculas_Es_Conflict()
    {
        using var e = new Entorno();
        await e.SembrarZonaAsync("Terraza");

        EsFallo(await e.Zonas.CrearAsync(new GuardarZonaRequest("terraza"), default), ErrorType.Conflict);
    }

    [Fact]
    public async Task Zona_Crear_Nombre_Vacio_Es_Validation()
    {
        using var e = new Entorno();

        EsFallo(await e.Zonas.CrearAsync(new GuardarZonaRequest("   "), default), ErrorType.Validation);
    }

    [Fact]
    public async Task Zona_Actualizar_Puede_Conservar_Su_Propio_Nombre()
    {
        using var e = new Entorno();
        var zona = await e.SembrarZonaAsync("Terraza");

        var resultado = await e.Zonas.ActualizarAsync(zona.Id, new GuardarZonaRequest("Terraza"), default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Zona_Actualizar_Con_Nombre_De_Otra_Es_Conflict()
    {
        using var e = new Entorno();
        await e.SembrarZonaAsync("Salón");
        var terraza = await e.SembrarZonaAsync("Terraza");

        EsFallo(await e.Zonas.ActualizarAsync(terraza.Id, new GuardarZonaRequest("Salón"), default), ErrorType.Conflict);
    }

    [Fact]
    public async Task Zona_Obtener_Inexistente_Es_NotFound()
    {
        using var e = new Entorno();

        EsFallo(await e.Zonas.ObtenerAsync(Guid.NewGuid(), default), ErrorType.NotFound);
    }

    [Fact]
    public async Task Zona_Desactivar_Con_Reserva_Futura_En_Sus_Mesas_Es_Conflict()
    {
        using var e = new Entorno();
        var zona = await e.SembrarZonaAsync();
        var mesa = await e.SembrarMesaAsync(zona.Id);
        var franja = await e.SembrarFranjaAsync("Cena", 18, 22);
        await e.SembrarReservaAsync(mesa.Id, franja.Id, Futuro);

        EsFallo(await e.Zonas.CambiarEstadoAsync(zona.Id, false, default), ErrorType.Conflict);
    }

    [Fact]
    public async Task Zona_Desactivar_Sin_Reservas_Futuras_Funciona()
    {
        using var e = new Entorno();
        var zona = await e.SembrarZonaAsync();
        var mesa = await e.SembrarMesaAsync(zona.Id);
        var franja = await e.SembrarFranjaAsync("Cena", 18, 22);
        await e.SembrarReservaAsync(mesa.Id, franja.Id, Pasado);                    // pasada: no bloquea
        await e.SembrarReservaAsync(mesa.Id, franja.Id, Futuro, cancelada: true);   // cancelada: no bloquea

        var resultado = await e.Zonas.CambiarEstadoAsync(zona.Id, false, default);

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value!.Activa);
    }

    [Fact]
    public async Task Zona_Listar_Filtra_Por_Estado_Y_Valida_PageSize()
    {
        using var e = new Entorno();
        await e.SembrarZonaAsync("Activa");
        await e.SembrarZonaAsync("Inactiva", activa: false);

        var soloInactivas = await e.Zonas.ListarAsync(new ListarSalonRequest { Activa = false }, default);
        var excedida = await e.Zonas.ListarAsync(new ListarSalonRequest { PageSize = 101 }, default);

        Assert.Equal("Inactiva", Assert.Single(soloInactivas.Value!.Items).Nombre);
        EsFallo(excedida, ErrorType.Validation);
    }

    // ---- Mesas (GI-194)

    [Fact]
    public async Task Mesa_Crear_Devuelve_El_Nombre_De_La_Zona()
    {
        using var e = new Entorno();
        var zona = await e.SembrarZonaAsync("Terraza");

        var resultado = await e.Mesas.CrearAsync(new GuardarMesaRequest(7, 4, zona.Id), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Terraza", resultado.Value!.ZonaNombre);
        Assert.True(resultado.Value.Activa);
    }

    [Fact]
    public async Task Mesa_Crear_Numero_Repetido_En_Cualquier_Zona_Es_Conflict()
    {
        using var e = new Entorno();
        var salon = await e.SembrarZonaAsync("Salón");
        var terraza = await e.SembrarZonaAsync("Terraza");
        await e.SembrarMesaAsync(salon.Id, numero: 1);

        var repetida = await e.Mesas.CrearAsync(new GuardarMesaRequest(1, 4, salon.Id), default);
        var otraZona = await e.Mesas.CrearAsync(new GuardarMesaRequest(1, 4, terraza.Id), default);

        EsFallo(repetida, ErrorType.Conflict);
        EsFallo(otraZona, ErrorType.Conflict);
        Assert.Equal("Ya existe la mesa 1.", otraZona.Error);
    }

    [Fact]
    public async Task Mesa_Actualizar_A_Numero_De_Otra_Zona_Es_Conflict()
    {
        using var e = new Entorno();
        var salon = await e.SembrarZonaAsync("Salón");
        var terraza = await e.SembrarZonaAsync("Terraza");
        await e.SembrarMesaAsync(salon.Id, numero: 1);
        var mesaTerraza = await e.SembrarMesaAsync(terraza.Id, numero: 2);

        var resultado = await e.Mesas.ActualizarAsync(mesaTerraza.Id, new GuardarMesaRequest(1, 4, terraza.Id), default);

        EsFallo(resultado, ErrorType.Conflict);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(51)]
    public async Task Mesa_Crear_Con_Capacidad_Fuera_De_Rango_Es_Validation(int capacidad)
    {
        using var e = new Entorno();
        var zona = await e.SembrarZonaAsync();

        EsFallo(await e.Mesas.CrearAsync(new GuardarMesaRequest(1, capacidad, zona.Id), default), ErrorType.Validation);
    }

    [Fact]
    public async Task Mesa_Crear_En_Zona_Inexistente_Es_Validation_Y_En_Zona_Inactiva_Es_Conflict()
    {
        using var e = new Entorno();
        var inactiva = await e.SembrarZonaAsync("Cerrada", activa: false);

        var inexistente = await e.Mesas.CrearAsync(new GuardarMesaRequest(1, 4, Guid.NewGuid()), default);
        var enInactiva = await e.Mesas.CrearAsync(new GuardarMesaRequest(1, 4, inactiva.Id), default);

        EsFallo(inexistente, ErrorType.Validation);
        EsFallo(enInactiva, ErrorType.Conflict);
    }

    [Fact]
    public async Task Mesa_Desactivar_Con_Reserva_Futura_Vigente_Es_Conflict()
    {
        using var e = new Entorno();
        var zona = await e.SembrarZonaAsync();
        var mesa = await e.SembrarMesaAsync(zona.Id);
        var franja = await e.SembrarFranjaAsync("Cena", 18, 22);
        await e.SembrarReservaAsync(mesa.Id, franja.Id, Futuro);

        EsFallo(await e.Mesas.CambiarEstadoAsync(mesa.Id, false, default), ErrorType.Conflict);
        Assert.True((await e.Contexto.Mesas.FindAsync(mesa.Id))!.Activa);
    }

    [Fact]
    public async Task Mesa_Desactivar_Ignora_Reservas_Pasadas_O_Canceladas()
    {
        using var e = new Entorno();
        var zona = await e.SembrarZonaAsync();
        var mesa = await e.SembrarMesaAsync(zona.Id);
        var franja = await e.SembrarFranjaAsync("Cena", 18, 22);
        await e.SembrarReservaAsync(mesa.Id, franja.Id, Pasado);
        await e.SembrarReservaAsync(mesa.Id, franja.Id, Futuro, cancelada: true);

        var resultado = await e.Mesas.CambiarEstadoAsync(mesa.Id, false, default);

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value!.Activa);
    }

    [Fact]
    public async Task Mesa_Reducir_Capacidad_Por_Debajo_De_Una_Reserva_Futura_Es_Conflict()
    {
        using var e = new Entorno();
        var zona = await e.SembrarZonaAsync();
        var mesa = await e.SembrarMesaAsync(zona.Id, capacidad: 6);
        var franja = await e.SembrarFranjaAsync("Cena", 18, 22);
        await e.SembrarReservaAsync(mesa.Id, franja.Id, Futuro, personas: 5);

        var rechazada = await e.Mesas.ActualizarAsync(mesa.Id, new GuardarMesaRequest(1, 4, zona.Id), default);
        var aceptada = await e.Mesas.ActualizarAsync(mesa.Id, new GuardarMesaRequest(1, 5, zona.Id), default);

        EsFallo(rechazada, ErrorType.Conflict);
        Assert.True(aceptada.IsSuccess);
    }

    [Fact]
    public async Task Mesa_Mover_A_Zona_Inactiva_Es_Conflict_Pero_Editar_En_Su_Zona_Inactiva_Si_Se_Permite()
    {
        using var e = new Entorno();
        var activa = await e.SembrarZonaAsync("Activa");
        var inactiva = await e.SembrarZonaAsync("Inactiva", activa: false);
        var mesaEnActiva = await e.SembrarMesaAsync(activa.Id, numero: 1);
        var mesaEnInactiva = await e.SembrarMesaAsync(inactiva.Id, numero: 2);

        var mover = await e.Mesas.ActualizarAsync(mesaEnActiva.Id, new GuardarMesaRequest(1, 4, inactiva.Id), default);
        var editar = await e.Mesas.ActualizarAsync(mesaEnInactiva.Id, new GuardarMesaRequest(2, 8, inactiva.Id), default);

        EsFallo(mover, ErrorType.Conflict);
        Assert.True(editar.IsSuccess);
        Assert.Equal(8, editar.Value!.Capacidad);
    }

    [Fact]
    public async Task Mesa_Listar_Filtra_Por_Zona()
    {
        using var e = new Entorno();
        var salon = await e.SembrarZonaAsync("Salón");
        var terraza = await e.SembrarZonaAsync("Terraza");
        await e.SembrarMesaAsync(salon.Id, numero: 1);
        await e.SembrarMesaAsync(terraza.Id, numero: 2);

        var resultado = await e.Mesas.ListarAsync(new ListarMesasRequest { ZonaId = terraza.Id }, default);

        var mesa = Assert.Single(resultado.Value!.Items);
        Assert.Equal(2, mesa.Numero);
        Assert.Equal("Terraza", mesa.ZonaNombre);
    }

    // ---- Franjas (GI-195)

    [Fact]
    public async Task Franja_Crear_Valida_Funciona()
    {
        using var e = new Entorno();

        var resultado = await e.Franjas.CrearAsync(
            new GuardarFranjaRequest("Brunch", new TimeOnly(9, 0), new TimeOnly(11, 30)), default);

        Assert.True(resultado.IsSuccess);
        Assert.True(resultado.Value!.Activa);
    }

    [Theory]
    [InlineData(12, 12)]
    [InlineData(15, 12)]
    public async Task Franja_Crear_Con_Fin_No_Posterior_Al_Inicio_Es_Validation(int inicio, int fin)
    {
        using var e = new Entorno();

        var resultado = await e.Franjas.CrearAsync(
            new GuardarFranjaRequest("Rara", new TimeOnly(inicio, 0), new TimeOnly(fin, 0)), default);

        EsFallo(resultado, ErrorType.Validation);
    }

    [Fact]
    public async Task Franja_Crear_Solapada_Con_Una_Activa_Es_Conflict_Pero_Contigua_No()
    {
        using var e = new Entorno();
        await e.SembrarFranjaAsync("Almuerzo", 12, 15);

        var solapada = await e.Franjas.CrearAsync(
            new GuardarFranjaRequest("Tarde", new TimeOnly(14, 0), new TimeOnly(17, 0)), default);
        var contigua = await e.Franjas.CrearAsync(
            new GuardarFranjaRequest("Merienda", new TimeOnly(15, 0), new TimeOnly(17, 0)), default);

        EsFallo(solapada, ErrorType.Conflict);
        Assert.True(contigua.IsSuccess);
    }

    [Fact]
    public async Task Franja_Crear_Ignora_Solapamiento_Con_Franjas_Inactivas()
    {
        using var e = new Entorno();
        await e.SembrarFranjaAsync("Vieja", 12, 15, activa: false);

        var resultado = await e.Franjas.CrearAsync(
            new GuardarFranjaRequest("Nueva", new TimeOnly(12, 0), new TimeOnly(15, 0)), default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Franja_Crear_Nombre_Repetido_Es_Conflict()
    {
        using var e = new Entorno();
        await e.SembrarFranjaAsync("Almuerzo", 12, 15);

        var resultado = await e.Franjas.CrearAsync(
            new GuardarFranjaRequest("almuerzo", new TimeOnly(16, 0), new TimeOnly(17, 0)), default);

        EsFallo(resultado, ErrorType.Conflict);
    }

    [Fact]
    public async Task Franja_Cambiar_Horario_Con_Reserva_Futura_Es_Conflict_Pero_Cambiar_Solo_El_Nombre_No()
    {
        using var e = new Entorno();
        var zona = await e.SembrarZonaAsync();
        var mesa = await e.SembrarMesaAsync(zona.Id);
        var franja = await e.SembrarFranjaAsync("Cena", 18, 22);
        await e.SembrarReservaAsync(mesa.Id, franja.Id, Futuro);

        var horario = await e.Franjas.ActualizarAsync(
            franja.Id, new GuardarFranjaRequest("Cena", new TimeOnly(19, 0), new TimeOnly(22, 0)), default);
        var nombre = await e.Franjas.ActualizarAsync(
            franja.Id, new GuardarFranjaRequest("Cena tardía", new TimeOnly(18, 0), new TimeOnly(22, 0)), default);

        EsFallo(horario, ErrorType.Conflict);
        Assert.True(nombre.IsSuccess);
    }

    [Fact]
    public async Task Franja_Desactivar_Con_Reserva_Futura_Es_Conflict()
    {
        using var e = new Entorno();
        var zona = await e.SembrarZonaAsync();
        var mesa = await e.SembrarMesaAsync(zona.Id);
        var franja = await e.SembrarFranjaAsync("Cena", 18, 22);
        await e.SembrarReservaAsync(mesa.Id, franja.Id, Futuro);

        EsFallo(await e.Franjas.CambiarEstadoAsync(franja.Id, false, default), ErrorType.Conflict);
    }

    [Fact]
    public async Task Franja_Reactivar_Que_Se_Solapa_Con_Una_Activa_Es_Conflict()
    {
        using var e = new Entorno();
        var inactiva = await e.SembrarFranjaAsync("Vieja", 12, 15, activa: false);
        await e.SembrarFranjaAsync("Almuerzo", 13, 16);

        EsFallo(await e.Franjas.CambiarEstadoAsync(inactiva.Id, true, default), ErrorType.Conflict);
    }

    [Fact]
    public async Task Franja_Listar_Ordena_Por_Hora_De_Inicio()
    {
        using var e = new Entorno();
        await e.SembrarFranjaAsync("Cena", 18, 22);
        await e.SembrarFranjaAsync("Almuerzo", 12, 15);

        var resultado = await e.Franjas.ListarAsync(new ListarSalonRequest(), default);

        Assert.Equal(new[] { "Almuerzo", "Cena" }, resultado.Value!.Items.Select(f => f.Nombre).ToArray());
    }
}