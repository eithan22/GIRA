using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Services.Seguridad.Auth;
using GIRA.Tests.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas HTTP de POST /api/reservas para el camino del personal (GI-222/GI-234/GI-235):
/// recorren el pipeline real (autenticación JWT, autorización por política OperarReservas,
/// rate limiting, mapeo a ProblemDetails) contra una base en memoria propia por prueba
/// (GiraApiFactory, EF Core InMemory — no SQL Server, no necesita Docker ni User Secrets).
/// </summary>
/// <remarks>
/// No cubre el índice único de doble reserva ni el token de concurrencia (RowVersion): el
/// proveedor InMemory no los aplica igual que SQL Server. Eso lo prueba GI-228 contra una
/// base real. Estas pruebas dependen de que GiraApiFactory conecte
/// AuditoriaSaveChangesInterceptor para que CreatedBy se rellene.
///
/// Origen viaja en el JSON como número (enum sin JsonStringEnumConverter global en el
/// proyecto): Telefono = 1, Presencial = 2. El frontend (GI-209) debe mandarlo así, o habrá
/// que agregar el conversor a texto más adelante — no se asume aquí.
/// </remarks>
public class CrearReservaComoPersonalHttpTests : IAsyncLifetime
{
    private const string Url = "/api/reservas";
    private static readonly DateOnly Fecha = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);
    private static readonly TimeOnly HoraInicioFranja = new(12, 0);
    private static readonly TimeOnly HoraFinFranja = new(15, 0);

    private readonly GiraApiFactory _factory = new();
    private HttpClient _cliente = null!;
    private Guid _franjaId;

    public async Task InitializeAsync()
    {
        _cliente = _factory.CreateClient();
        _franjaId = await SembrarZonaYFranjaAsync();
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task<Guid> SembrarZonaYFranjaAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();

        var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Salón" };
        var franja = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Almuerzo", HoraInicio = HoraInicioFranja, HoraFin = HoraFinFranja
        };
        contexto.Zonas.Add(zona);
        contexto.FranjasHorarias.Add(franja);
        await contexto.SaveChangesAsync();

        return franja.Id;
    }

    // Cada prueba siembra su propia mesa, con capacidad == personas (patrón de GI-228):
    // innecesario aquí porque cada prueba ya tiene su propia base InMemory aislada, pero se
    // mantiene como refuerzo y para que la intención quede explícita.
    private async Task SembrarMesaAsync(int capacidad)
    {
        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();

        var zonaId = await contexto.Zonas.Select(z => z.Id).FirstAsync();
        contexto.Mesas.Add(new Mesa { Id = Guid.NewGuid(), Numero = capacidad, Capacidad = capacidad, ZonaId = zonaId });
        await contexto.SaveChangesAsync();
    }

    // Token minteado directo con ITokenService: nunca pasa por /api/auth/login, no necesita
    // contraseña ni un Usuario real en Identity (la validación JWT no consulta la base; ver
    // AutenticacionJwtExtensions.cs, sin eventos OnTokenValidated).
    private string GenerarToken(Guid usuarioId, params string[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokenService.GenerarAccessToken(usuarioId, "personal@gira.local", "Personal de Prueba", roles).Token;
    }

    // Token válido (misma firma, emisor y audiencia que ITokenService) pero sin el claim
    // "sub" (UsuarioId): construido a mano porque ITokenService siempre lo incluye. Prueba
    // que el controller nunca crea con CreatedBy desconocido; responde 401, no 500.
    private string GenerarTokenSinUsuarioId(params string[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtSettings>();
        var ahora = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(TiposClaim.Email, "sin-usuarioid@gira.local"),
            new(TiposClaim.Nombre, "Sin UsuarioId")
        };
        claims.AddRange(roles.Select(rol => new Claim(TiposClaim.Rol, rol)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = ahora,
            NotBefore = ahora,
            Expires = ahora.AddMinutes(jwt.AccessTokenMinutos),
            SigningCredentials = new SigningCredentials(jwt.ClaveFirma(), SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    // Corrompe la firma cambiando un carácter del MEDIO (nunca el último: en base64url el
    // último carácter puede tener bits sin usar que no siempre cambian el byte decodificado
    // si se reemplaza por el carácter equivocado). Un carácter del medio siempre corresponde
    // a un byte completo de la firma, así que el cambio es garantizado.
    private static string CorromperFirma(string token)
    {
        var partes = token.Split('.');
        var firma = partes[2];
        var indice = firma.Length / 2;
        var reemplazo = firma[indice] == 'A' ? 'B' : 'A'; // garantizado distinto del original
        var firmaCorrupta = firma[..indice] + reemplazo + firma[(indice + 1)..];

        return $"{partes[0]}.{partes[1]}.{firmaCorrupta}";
    }

    private static CrearReservaRequest Request(string telefono, int personas, Guid franjaId, OrigenReserva? origen) =>
        new("Cliente de Prueba", telefono, Fecha, franjaId, personas, origen);

    [Fact]
    public async Task Crear_SinCabeceraAuthorization_FijaOrigenWebIgnorandoElDelCuerpo()
    {
        await SembrarMesaAsync(capacidad: 11);
        // Aunque el cuerpo mande Origen=Telefono, sin token debe ignorarse por completo.
        var request = Request("8091110001", 11, _franjaId, OrigenReserva.Telefono);

        var respuesta = await _cliente.PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var dto = await respuesta.Content.ReadFromJsonAsync<ReservaCreadaDto>();
        var (origen, createdBy) = await LeerReservaAsync(dto!.Codigo);
        Assert.Equal(OrigenReserva.Web, origen);
        Assert.Null(createdBy);
    }

    [Fact]
    public async Task Crear_ConTokenYOperarReservasOrigenTelefono_CreaConOrigenYCreatedByDelToken()
    {
        await SembrarMesaAsync(capacidad: 12);
        var usuarioId = Guid.NewGuid();
        _cliente.UsarToken(GenerarToken(usuarioId, Roles.Mesero));
        var request = Request("8091110002", 12, _franjaId, OrigenReserva.Telefono);

        var respuesta = await _cliente.PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var dto = await respuesta.Content.ReadFromJsonAsync<ReservaCreadaDto>();
        var (origen, createdBy) = await LeerReservaAsync(dto!.Codigo);
        Assert.Equal(OrigenReserva.Telefono, origen);
        Assert.Equal(usuarioId, createdBy);
    }

    [Fact]
    public async Task Crear_ConTokenYOperarReservasOrigenPresencial_CreaConOrigenPresencial()
    {
        await SembrarMesaAsync(capacidad: 13);
        var usuarioId = Guid.NewGuid();
        _cliente.UsarToken(GenerarToken(usuarioId, Roles.Supervisor));
        var request = Request("8091110003", 13, _franjaId, OrigenReserva.Presencial);

        var respuesta = await _cliente.PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var dto = await respuesta.Content.ReadFromJsonAsync<ReservaCreadaDto>();
        var (origen, createdBy) = await LeerReservaAsync(dto!.Codigo);
        Assert.Equal(OrigenReserva.Presencial, origen);
        Assert.Equal(usuarioId, createdBy);
    }

    [Fact]
    public async Task Crear_TokenSinPermiso_Devuelve403YNoCreaNada()
    {
        await SembrarMesaAsync(capacidad: 14);
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Cocina)); // fuera de OperarReservas
        var request = Request("8091110004", 14, _franjaId, OrigenReserva.Telefono);

        var respuesta = await _cliente.PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal(0, await ContarReservasAsync());
    }

    [Fact]
    public async Task Crear_CabeceraConFirmaInvalida_Devuelve401YNoSigueComoAnonimoNiCreaNada()
    {
        await SembrarMesaAsync(capacidad: 15);
        var tokenValido = GenerarToken(Guid.NewGuid(), Roles.Mesero);
        _cliente.UsarToken(CorromperFirma(tokenValido));
        var request = Request("8091110005", 15, _franjaId, OrigenReserva.Telefono);

        var respuesta = await _cliente.PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Contains(respuesta.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
        Assert.Equal(0, await ContarReservasAsync());
    }

    [Fact]
    public async Task Crear_TokenValidoSinClaimUsuarioId_Devuelve401NuncaQuinientos()
    {
        await SembrarMesaAsync(capacidad: 17);
        _cliente.UsarToken(GenerarTokenSinUsuarioId(Roles.Mesero)); // rol válido, pero sin "sub"
        var request = Request("8091110007", 17, _franjaId, OrigenReserva.Telefono);

        var respuesta = await _cliente.PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal(0, await ContarReservasAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(OrigenReserva.Web)]
    public async Task Crear_ConTokenYOperarReservasOrigenInvalido_Devuelve400YNoCreaNada(OrigenReserva? origen)
    {
        await SembrarMesaAsync(capacidad: 16);
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Gerente));
        var request = Request("8091110006", 16, _franjaId, origen);

        var respuesta = await _cliente.PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0, await ContarReservasAsync());
    }

    private async Task<(OrigenReserva Origen, Guid? CreatedBy)> LeerReservaAsync(string codigo)
    {
        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var reserva = await contexto.Reservas.FirstAsync(r => r.Codigo == codigo);
        return (reserva.Origen, reserva.CreatedBy);
    }

    private async Task<int> ContarReservasAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        return await contexto.Reservas.CountAsync();
    }
}
