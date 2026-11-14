using System.Security.Cryptography;
using GIRA.Application;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Antes de cualquier prueba de la clase, borra y vuelve a migrar GIRA_GI228_TEST una sola
/// vez, para que la corrida empiece siempre vacía. El seguro del nombre exacto de la base va
/// antes de borrar nada.
/// </summary>
public class ReservaServiceIntegrationFixture : IAsyncLifetime
{
    /// <summary>Cadena de conexión ya verificada; las pruebas la reciben a través del fixture.</summary>
    public string Conexion { get; private set; } = string.Empty;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<ReservaServiceIntegrationFixture>()
            .Build();

        var conexion = configuracion.GetConnectionString("GiraGi228Test");
        if (string.IsNullOrWhiteSpace(conexion))
        {
            throw new InvalidOperationException(
                "Falta la cadena de conexión 'GiraGi228Test' en los User Secrets de GIRA.Tests.");
        }

        var datosConexion = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(conexion);

        // Seguro: solo se borra si el nombre de la base es, exactamente, la de pruebas de GI-228.
        if (!string.Equals(datosConexion.InitialCatalog, "GIRA_GI228_TEST", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Las pruebas GI-228 solo pueden utilizar GIRA_GI228_TEST.");
        }

        Conexion = conexion;

        var opciones = new DbContextOptionsBuilder<GiraDbContext>().UseSqlServer(conexion).Options;
        await using var db = new GiraDbContext(opciones);

        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        // Las pruebas usan mesas de 11 a 13 personas: el tamaño máximo de grupo por defecto (8) las rechazaría.
        await ConfiguracionDePrueba.SembrarAsync(db);
    }

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// Caso crítico 1 del SRS (GI-228) y escenarios relacionados, contra SQL Server real. Sigue
/// el mismo patrón de base aislada que RefreshTokenIntegrationTests (GI-96): sin
/// Testcontainers, una base de pruebas propia (GIRA_GI228_TEST) sobre el SQL Server de
/// Docker. Cada prueba usa una capacidad de mesa, fecha y franja propias y distintas de las
/// demás, para quedar aislada de ellas dentro de la misma corrida (el fixture solo limpia
/// la base una vez por clase, no antes de cada prueba).
/// </summary>
[Trait("Category", "Integration")]
public class ReservaServiceIntegrationTests : IClassFixture<ReservaServiceIntegrationFixture>
{
    private static readonly Guid FranjaAlmuerzoId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid FranjaCenaId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    // IX_Mesa_Numero es único en toda la tabla reservas.Mesa y las pruebas de la clase comparten
    // la misma base (el fixture solo la recrea una vez por ejecución), así que cada prueba
    // necesita números de mesa propios que no se repitan con los de ninguna otra.
    private const int NumeroMesaDosSolicitudesSimultaneas = 9001;
    private const int NumeroMesaAlmuerzo = 9002;
    private const int NumeroMesaCena = 9003;
    private const int NumeroMesaLimiteDosReservas = 9004;

    private readonly string _conexion;

    /// <summary>Recibe la conexión ya verificada por el fixture compartido de la clase.</summary>
    public ReservaServiceIntegrationTests(ReservaServiceIntegrationFixture fixture)
    {
        _conexion = fixture.Conexion;
    }

    /// <summary>
    /// Teléfono válido (809 + 7 dígitos) distinto en cada llamada: aunque el fixture limpia
    /// la base una vez por clase, varias pruebas comparten esa misma base dentro de la
    /// corrida, así que cada una necesita su propio teléfono.
    /// </summary>
    private static string GenerarTelefonoUnico() => "809" + RandomNumberGenerator.GetInt32(1000000, 9999999);

    private static ServiceProvider CrearProveedor(string conexion)
    {
        var datosConexion = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(conexion);
        if (!string.Equals(datosConexion.InitialCatalog, "GIRA_GI228_TEST", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Las pruebas GI-228 solo pueden utilizar GIRA_GI228_TEST.");
        }

        var configuracionServicios = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GiraDb"] = conexion,
                ["Jwt:Issuer"] = "GIRA.Tests",
                ["Jwt:Audience"] = "GIRA.Tests",
                ["Jwt:Key"] = "clave-de-pruebas-de-gira-con-mas-de-32-caracteres",
                ["Jwt:AccessTokenMinutos"] = "30",
                ["Jwt:RefreshTokenDias"] = "7"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication();
        services.AddApplication();
        services.AddInfrastructure(configuracionServicios);

        return services.BuildServiceProvider();
    }

    // --- Migración Reservas_FranjasDeDosHoras (GI-226): sin DeleteData; solo toca franjas sin editar y sin uso ---

    private const string MigracionAnterior = "20261114001712_Configuracion_CrearTablas";

    private static readonly (string, string, string)[] FranjasDeDosHoras =
    [
        ("Almuerzo", "12:00", "13:45"), ("Tarde", "14:00", "15:45"), ("Cena", "18:30", "20:15"), ("Noche", "20:30", "22:15")
    ];

    private static readonly (string, string, string)[] FranjasOriginales =
    [
        ("Almuerzo", "12:00", "15:00"), ("Cena", "18:00", "22:30")
    ];

    private static async Task<List<(string Nombre, string Inicio, string Fin)>> LeerFranjasAsync(string conexion)
    {
        var opciones = new DbContextOptionsBuilder<GiraDbContext>().UseSqlServer(conexion).Options;
        await using var db = new GiraDbContext(opciones);
        var franjas = await db.FranjasHorarias.AsNoTracking().OrderBy(f => f.HoraInicio).ToListAsync();
        return franjas.Select(f => (f.Nombre, f.HoraInicio.ToString("HH:mm"), f.HoraFin.ToString("HH:mm"))).ToList();
    }

    [Fact]
    public async Task Migracion_FranjasDeDosHoras_ActualizaSoloSinEdicionesNiUso_Y_NuncaBorra()
    {
        var datosConexion = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_conexion);
        if (!string.Equals(datosConexion.InitialCatalog, "GIRA_GI228_TEST", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Las pruebas GI-228 solo pueden utilizar GIRA_GI228_TEST.");
        }

        var opciones = new DbContextOptionsBuilder<GiraDbContext>().UseSqlServer(_conexion).Options;
        await using var db = new GiraDbContext(opciones);
        var migrador = db.GetService<IMigrator>();
        var almuerzoId = "00000000-0000-0000-0000-000000000001";
        var sufijo = Guid.NewGuid().ToString("N");
        var zonaId = Guid.NewGuid();
        var mesaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var reservaId = Guid.NewGuid();

        try
        {
            // A. Base nueva (borrada y migrada por el fixture): las 2 originales, sin editar y sin uso, pasan a las 4 nuevas.
            Assert.Equal(FranjasDeDosHoras, await LeerFranjasAsync(_conexion));

            // B. Down con las 4 sin editar y sin uso: vuelve a las 2 originales (no borra ninguna de las que había).
            // Las otras pruebas de la clase ya dejaron reservas en esta base (solo de pruebas, validada arriba): con
            // reservas sobre las franjas la migración correctamente no toca nada, así que se parte de cero.
            await db.Database.ExecuteSqlRawAsync("DELETE FROM reservas.VisitaSinReserva; DELETE FROM reservas.Reserva;");
            await migrador.MigrateAsync(MigracionAnterior);
            Assert.Equal(FranjasOriginales, await LeerFranjasAsync(_conexion));

            // C. Una franja editada por el Gerente: Up no toca nada.
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE reservas.FranjaHoraria SET Nombre = N'Almuerzo largo', UpdatedAt = SYSUTCDATETIME() WHERE Id = '00000000-0000-0000-0000-000000000001'");
            await migrador.MigrateAsync();
            Assert.Equal(
                [("Almuerzo largo", "12:00", "15:00"), ("Cena", "18:00", "22:30")],
                await LeerFranjasAsync(_conexion));
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE reservas.FranjaHoraria SET Nombre = N'Almuerzo', UpdatedAt = NULL WHERE Id = '00000000-0000-0000-0000-000000000001'");

            // D. Una reserva usa Almuerzo (instalación en uso): Up no toca nada y la reserva sigue ahí.
            await migrador.MigrateAsync(MigracionAnterior);
            db.Zonas.Add(new Zona { Id = zonaId, Nombre = $"Zona-Migracion-{sufijo}" });
            db.Mesas.Add(new Mesa { Id = mesaId, Numero = 9801, Capacidad = 4, ZonaId = zonaId });
            db.Clientes.Add(new Cliente { Id = clienteId, Nombre = "Cliente Migración", Telefono = GenerarTelefonoUnico() });
            db.Reservas.Add(new Reserva
            {
                Id = reservaId, Codigo = sufijo[..8], ClienteId = clienteId, MesaId = mesaId,
                FranjaHorariaId = Guid.Parse(almuerzoId), Fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)),
                Personas = 2, Origen = GIRA.Domain.Enums.Reservas.OrigenReserva.Telefono
            });
            await db.SaveChangesAsync();
            await migrador.MigrateAsync();
            Assert.Equal(FranjasOriginales, await LeerFranjasAsync(_conexion));
            Assert.True(await db.Reservas.AsNoTracking().AnyAsync(r => r.Id == reservaId));

            // E. Sin la reserva y sin ediciones, otra vez pasa a las 4 nuevas.
            db.Reservas.Remove(await db.Reservas.SingleAsync(r => r.Id == reservaId));
            db.Mesas.Remove(await db.Mesas.SingleAsync(m => m.Id == mesaId));
            db.Clientes.Remove(await db.Clientes.SingleAsync(c => c.Id == clienteId));
            db.Zonas.Remove(await db.Zonas.SingleAsync(z => z.Id == zonaId));
            await db.SaveChangesAsync();
            await migrador.MigrateAsync(MigracionAnterior);
            await migrador.MigrateAsync();
            Assert.Equal(FranjasDeDosHoras, await LeerFranjasAsync(_conexion));
        }
        finally
        {
            // Pase lo que pase, la base de pruebas queda con todas las migraciones aplicadas.
            await migrador.MigrateAsync();
        }
    }

    [Fact]
    public async Task CrearAsync_DosSolicitudesSimultaneasMismaMesaFechaYFranja_SoloUnaExitosa()
    {
        using var proveedor = CrearProveedor(_conexion);

        // Capacidad 11, única de esta prueba: ninguna mesa de otra prueba de la clase puede
        // calificar para una solicitud de 11 personas (las demás usan 12 y 13).
        const int capacidad = 11;

        using (var scopeDatos = proveedor.CreateScope())
        {
            var db = scopeDatos.ServiceProvider.GetRequiredService<GiraDbContext>();
            var zona = new Zona { Id = Guid.NewGuid(), Nombre = $"Zona-GI228-mesa-{Guid.NewGuid():N}" };
            var mesa = new Mesa { Id = Guid.NewGuid(), Numero = NumeroMesaDosSolicitudesSimultaneas, Capacidad = capacidad, ZonaId = zona.Id };
            db.Zonas.Add(zona);
            db.Mesas.Add(mesa);
            await db.SaveChangesAsync();
        }

        var fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        using var scope1 = proveedor.CreateScope();
        using var scope2 = proveedor.CreateScope();

        var servicio1 = scope1.ServiceProvider.GetRequiredService<IReservaService>();
        var servicio2 = scope2.ServiceProvider.GetRequiredService<IReservaService>();

        var tarea1 = servicio1.CrearAsync(
            new CrearReservaRequest("Prueba Uno", GenerarTelefonoUnico(), fecha, FranjaAlmuerzoId, capacidad), default);
        var tarea2 = servicio2.CrearAsync(
            new CrearReservaRequest("Prueba Dos", GenerarTelefonoUnico(), fecha, FranjaAlmuerzoId, capacidad), default);

        var resultados = await Task.WhenAll(tarea1, tarea2);

        var exitos = resultados.Count(r => r.IsSuccess);
        var conflictos = resultados.Count(r => r.IsFailure && r.Type == ErrorType.Conflict);

        Assert.Equal(1, exitos);
        Assert.Equal(1, conflictos);

        // Verificación por el código de la solicitud que ganó (no por una mesa específica:
        // cuál mesa exacta elige el servicio no es lo que se prueba aquí).
        var codigoExitoso = resultados.Single(r => r.IsSuccess).Value!.Codigo;

        using var scopeVerificacion = proveedor.CreateScope();
        var dbVerificacion = scopeVerificacion.ServiceProvider.GetRequiredService<GiraDbContext>();

        var existeLaExitosa = await dbVerificacion.Reservas.AnyAsync(r => r.Codigo == codigoExitoso);
        Assert.True(existeLaExitosa);

        // Exactamente 1 reserva vigente en esa fecha y franja (propia de esta prueba).
        var vigentesEnEsaFechaYFranja = await dbVerificacion.Reservas
            .CountAsync(r => r.Fecha == fecha && r.FranjaHorariaId == FranjaAlmuerzoId);
        Assert.Equal(1, vigentesEnEsaFechaYFranja);
    }

    [Fact]
    public async Task CrearAsync_MismoTelefonoNuevoEnDosSolicitudesSimultaneas_QuedaUnSoloCliente()
    {
        using var proveedor = CrearProveedor(_conexion);

        // Capacidad 12, única de esta prueba.
        const int capacidad = 12;

        using (var scopeDatos = proveedor.CreateScope())
        {
            var db = scopeDatos.ServiceProvider.GetRequiredService<GiraDbContext>();

            var zona = new Zona { Id = Guid.NewGuid(), Nombre = $"Zona-GI228-telefono-{Guid.NewGuid():N}" };
            var mesaAlmuerzo = new Mesa { Id = Guid.NewGuid(), Numero = NumeroMesaAlmuerzo, Capacidad = capacidad, ZonaId = zona.Id };
            var mesaCena = new Mesa { Id = Guid.NewGuid(), Numero = NumeroMesaCena, Capacidad = capacidad, ZonaId = zona.Id };

            db.Zonas.Add(zona);
            db.Mesas.AddRange(mesaAlmuerzo, mesaCena);
            await db.SaveChangesAsync();
        }

        var fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(6));
        var telefono = GenerarTelefonoUnico();

        using var scope1 = proveedor.CreateScope();
        using var scope2 = proveedor.CreateScope();

        var servicio1 = scope1.ServiceProvider.GetRequiredService<IReservaService>();
        var servicio2 = scope2.ServiceProvider.GetRequiredService<IReservaService>();

        var tarea1 = servicio1.CrearAsync(
            new CrearReservaRequest("Cliente Carrera", telefono, fecha, FranjaAlmuerzoId, capacidad), default);
        var tarea2 = servicio2.CrearAsync(
            new CrearReservaRequest("Cliente Carrera", telefono, fecha, FranjaCenaId, capacidad), default);

        var resultados = await Task.WhenAll(tarea1, tarea2);

        Assert.All(resultados, r => Assert.True(r.IsSuccess));

        using var scopeVerificacion = proveedor.CreateScope();
        var dbVerificacion = scopeVerificacion.ServiceProvider.GetRequiredService<GiraDbContext>();

        var clientes = await dbVerificacion.Clientes.Where(c => c.Telefono == telefono).ToListAsync();
        Assert.Single(clientes);

        var reservasDelCliente = await dbVerificacion.Reservas
            .CountAsync(r => r.ClienteId == clientes[0].Id && r.Fecha == fecha);
        Assert.Equal(2, reservasDelCliente);
    }

    [Fact]
    public async Task CrearAsync_TercerReservaVigenteDelMismoTelefono_EsRechazadaYCanceladaNoCuenta()
    {
        using var proveedor = CrearProveedor(_conexion);

        // Capacidad 13, única de esta prueba.
        const int capacidad = 13;

        using (var scopeDatos = proveedor.CreateScope())
        {
            var db = scopeDatos.ServiceProvider.GetRequiredService<GiraDbContext>();
            var zona = new Zona { Id = Guid.NewGuid(), Nombre = $"Zona-GI228-limite-{Guid.NewGuid():N}" };
            var mesa = new Mesa { Id = Guid.NewGuid(), Numero = NumeroMesaLimiteDosReservas, Capacidad = capacidad, ZonaId = zona.Id };
            db.Zonas.Add(zona);
            db.Mesas.Add(mesa);
            await db.SaveChangesAsync();
        }

        var telefonoNormalizado = GenerarTelefonoUnico();
        // Mismo teléfono, escrito con guiones: debe normalizarse y contar igual.
        var telefonoConGuiones = $"{telefonoNormalizado[..3]}-{telefonoNormalizado.Substring(3, 3)}-{telefonoNormalizado[6..]}";

        var fecha1 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var fecha2 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        var fecha3 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(9));

        async Task<OperationResult<ReservaCreadaDto>> CrearEnSuPropioScopeAsync(string telefono, DateOnly fecha)
        {
            using var scope = proveedor.CreateScope();
            var servicio = scope.ServiceProvider.GetRequiredService<IReservaService>();
            return await servicio.CrearAsync(
                new CrearReservaRequest("Cliente Límite", telefono, fecha, FranjaAlmuerzoId, capacidad), default);
        }

        var reserva1 = await CrearEnSuPropioScopeAsync(telefonoNormalizado, fecha1);
        Assert.True(reserva1.IsSuccess);

        var reserva2 = await CrearEnSuPropioScopeAsync(telefonoConGuiones, fecha2);
        Assert.True(reserva2.IsSuccess);

        var reserva3Rechazada = await CrearEnSuPropioScopeAsync(telefonoNormalizado, fecha3);
        Assert.True(reserva3Rechazada.IsFailure);
        Assert.Equal(ErrorType.Conflict, reserva3Rechazada.Type);
        Assert.Contains("2 reservas vigentes", reserva3Rechazada.Error);

        // Cancelar la primera reserva: una reserva Cancelada no debe contar para el límite.
        using (var scopeCancelar = proveedor.CreateScope())
        {
            var db = scopeCancelar.ServiceProvider.GetRequiredService<GiraDbContext>();
            var reservaGuardada = await db.Reservas.SingleAsync(r => r.Codigo == reserva1.Value!.Codigo);
            reservaGuardada.Cancelar("Prueba de integración GI-228");
            await db.SaveChangesAsync();
        }

        var reserva3Reintentada = await CrearEnSuPropioScopeAsync(telefonoNormalizado, fecha3);
        Assert.True(reserva3Reintentada.IsSuccess);
    }
}
