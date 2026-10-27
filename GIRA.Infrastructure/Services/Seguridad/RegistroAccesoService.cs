using GIRA.Application.Interfaces.Services.Seguridad.Accesos;
using GIRA.Domain.Entities.Seguridad;
using GIRA.Infrastructure.Persistence.Context;

namespace GIRA.Infrastructure.Services.Seguridad;

public class RegistroAccesoService : IRegistroAccesoService
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el servicio con el contexto de base de datos.</summary>
    public RegistroAccesoService(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task RegistrarAsync(Guid? usuarioId, string correo, string ip, bool exitoso, string? motivo, CancellationToken cancellationToken = default)
    {
        var registro = new RegistroAcceso
        {
            UsuarioId = usuarioId,
            CorreoIntentado = Recortar(correo, 256),
            Fecha = DateTime.UtcNow,
            Ip = Recortar(ip, 45),
            Exitoso = exitoso,
            Motivo = motivo is null ? null : Recortar(motivo, 200)
        };

        _contexto.RegistrosAcceso.Add(registro);
        await _contexto.SaveChangesAsync(cancellationToken);
    }

    // Los longitudes coinciden con RegistroAccesoConfiguration; un valor más largo se recorta en vez de fallar el login.
    private static string Recortar(string valor, int maximo) =>
        valor.Length <= maximo ? valor : valor[..maximo];
}
