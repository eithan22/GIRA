using System.Diagnostics;
using GIRA.Application.Common;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Analitica;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Analitica;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace GIRA.Tests.Analitica;

[Trait("Category", "Integration")]
public sealed class DashboardRendimientoSqlTests(ITestOutputHelper salida)
{
    [Fact]
    public async Task Dashboard_ConDoceMesesEnSqlServer_CargaEnMenosDeTresSegundos()
    {
        var origen = new ConfigurationBuilder().AddUserSecrets<Program>().Build().GetConnectionString("GiraDb")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:GiraDb en los secretos de la API.");
        var nombre = "GIRA_ANALITICA_BENCH_" + Guid.NewGuid().ToString("N");
        var datos = new SqlConnectionStringBuilder(origen) { InitialCatalog = nombre };
        var conexion = datos.ConnectionString;
        var opciones = new DbContextOptionsBuilder<GiraDbContext>().UseSqlServer(conexion).Options;
        await using var db = new GiraDbContext(opciones);
        var creada = false;
        try
        {
            var master = new SqlConnectionStringBuilder(origen) { InitialCatalog = "master" };
            await using (var comprobacion = new SqlConnection(master.ConnectionString))
            {
                await comprobacion.OpenAsync();
                await using var comando = comprobacion.CreateCommand();
                comando.CommandText = $"CREATE DATABASE [{nombre}]";
                await comando.ExecuteNonQueryAsync();
                creada = true;
            }
            if (!await db.Database.EnsureCreatedAsync())
                throw new InvalidOperationException("No se pudo crear el esquema de la base aislada.");
            var hoy = TimeProvider.System.HoyLocal();
            var franjas = Enumerable.Range(0, 4).Select(i => new FranjaHoraria
            {
                Id = Guid.NewGuid(), Nombre = $"Franja de prueba {i}", HoraInicio = new TimeOnly(10 + i * 2, 0),
                HoraFin = new TimeOnly(12 + i * 2, 0), Activa = true
            }).ToArray();
            db.FranjasHorarias.AddRange(franjas);
            await db.SaveChangesAsync();
            for (var dia = 0; dia < 365; dia++)
            {
                foreach (var franja in franjas)
                {
                    for (var evento = 0; evento < 10; evento++)
                        db.Set<RegistroAfluencia>().Add(new RegistroAfluencia
                        {
                            Id = Guid.NewGuid(), ReferenciaId = Guid.NewGuid(), Fecha = hoy.AddDays(-dia),
                            FranjaHorariaId = franja.Id, Origen = OrigenRegistroAfluencia.VisitaSinReserva,
                            Tipo = TipoRegistroAfluencia.VisitaCompletada, Personas = 2
                        });
                }
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            await using var fabrica = new Fabrica(conexion);
            fabrica.UseKestrel(0);
            using var cliente = fabrica.CreateClient();
            using var scope = fabrica.Services.CreateScope();
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            var token = tokens.GenerarAccessToken(Guid.NewGuid(), "prueba@gira.local", "Prueba", [Roles.Gerente]).Token;
            var frontend = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Gira.frontend"));
            var proceso = new ProcessStartInfo
            {
                FileName = "node", WorkingDirectory = frontend, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            proceso.ArgumentList.Add(Path.Combine("tests", "dashboard.performance.mjs"));
            proceso.Environment["GIRA_BENCH_API"] = cliente.BaseAddress!.ToString();
            proceso.Environment["GIRA_BENCH_TOKEN"] = token;
            var navegador = BuscarNavegador();
            if (navegador is not null)
                proceso.Environment["PLAYWRIGHT_CHROME_EXECUTABLE_PATH"] = navegador;
            using var ejecutado = Process.Start(proceso) ?? throw new InvalidOperationException("No se pudo iniciar la prueba del navegador.");
            var stdout = ejecutado.StandardOutput.ReadToEndAsync();
            var stderr = ejecutado.StandardError.ReadToEndAsync();
            try
            {
                await ejecutado.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
            }
            catch (TimeoutException)
            {
                ejecutado.Kill(entireProcessTree: true);
                await ejecutado.WaitForExitAsync();
                throw;
            }
            var resultado = await stdout;
            var errores = await stderr;
            salida.WriteLine(resultado);
            Assert.True(ejecutado.ExitCode == 0, $"La prueba de rendimiento falló: {errores}");
        }
        finally
        {
            if (creada && nombre.StartsWith("GIRA_ANALITICA_BENCH_", StringComparison.Ordinal)
                && Guid.TryParseExact(nombre["GIRA_ANALITICA_BENCH_".Length..], "N", out _))
                await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Entrenamiento_ConDosReclamosSimultaneos_SeIniciaUnaSolaVez()
    {
        var origen = new ConfigurationBuilder().AddUserSecrets<Program>().Build().GetConnectionString("GiraDb")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:GiraDb en los secretos de la API.");
        var nombre = "GIRA_ANALITICA_CLAIM_" + Guid.NewGuid().ToString("N");
        var conexion = new SqlConnectionStringBuilder(origen) { InitialCatalog = nombre }.ConnectionString;
        var opciones = new DbContextOptionsBuilder<GiraDbContext>().UseSqlServer(conexion).Options;
        await using var db = new GiraDbContext(opciones);
        var creada = false;
        try
        {
            var master = new SqlConnectionStringBuilder(origen) { InitialCatalog = "master" };
            await using (var comprobacion = new SqlConnection(master.ConnectionString))
            {
                await comprobacion.OpenAsync();
                await using var comando = comprobacion.CreateCommand();
                comando.CommandText = $"CREATE DATABASE [{nombre}]";
                await comando.ExecuteNonQueryAsync();
                creada = true;
            }

            Assert.True(await db.Database.EnsureCreatedAsync());
            var trabajo = new EntrenamientoModelo
            {
                Id = Guid.NewGuid(),
                SolicitadoPor = Guid.NewGuid(),
                Estado = EstadoEntrenamientoModelo.EnCola,
                SolicitadoEn = DateTimeOffset.UtcNow
            };
            db.Set<EntrenamientoModelo>().Add(trabajo);
            await db.SaveChangesAsync();

            var instante = DateTimeOffset.UtcNow;
            await using var contextoA = new GiraDbContext(opciones);
            await using var contextoB = new GiraDbContext(opciones);
            var reclamos = await Task.WhenAll(
                new ModelosAfluenciaRepository(contextoA).IniciarSiguienteEntrenamientoAsync(instante, CancellationToken.None),
                new ModelosAfluenciaRepository(contextoB).IniciarSiguienteEntrenamientoAsync(instante, CancellationToken.None));

            Assert.Single(reclamos, resultado => resultado is not null);
            Assert.Single(reclamos, resultado => resultado is null);
            Assert.Equal(EstadoEntrenamientoModelo.EnCurso,
                (await db.Set<EntrenamientoModelo>().AsNoTracking().SingleAsync()).Estado);
        }
        finally
        {
            if (creada && nombre.StartsWith("GIRA_ANALITICA_CLAIM_", StringComparison.Ordinal)
                && Guid.TryParseExact(nombre["GIRA_ANALITICA_CLAIM_".Length..], "N", out _))
                await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class Fabrica(string conexion) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:GiraDb", conexion);
            builder.UseSetting("Jwt:Key", "clave-de-pruebas-de-gira-con-mas-de-32-caracteres");
            builder.UseSetting("Reservas:ProcesoAutomatico:Habilitado", "false");
            builder.UseSetting("Inventario:ProcesoVencimientos:Habilitado", "false");
            builder.UseSetting("Analitica:Generacion:Habilitada", "false");
            builder.UseSetting("Analitica:Recomendaciones:Habilitada", "false");
            builder.UseSetting("Analitica:Entrenamiento:Habilitada", "false");
        }

    }

    private static string? BuscarNavegador()
    {
        var candidatos = new[]
        {
            Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROME_EXECUTABLE_PATH"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google", "Chrome", "Application", "chrome.exe")
        };
        return candidatos.FirstOrDefault(ruta => !string.IsNullOrWhiteSpace(ruta) && File.Exists(ruta));
    }
}
