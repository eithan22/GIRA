using GIRA.Application.Common;
using GIRA.Domain.Entities.Reservas;

namespace GIRA.Application.Interfaces.Repository.Reservas;

/// <summary>Lecturas y escrituras de <see cref="Cliente"/> que necesita el módulo Reservas.</summary>
public interface IClienteRepository
{
    /// <summary>Busca un cliente por su teléfono normalizado (10 dígitos). Null si no existe.</summary>
    Task<Cliente?> BuscarPorTelefonoAsync(string telefono, CancellationToken cancellationToken);

    /// <summary>Busca un cliente por su Id. Null si no existe.</summary>
    Task<Cliente?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Busca clientes (GI-164) cuyo nombre contenga el texto (sin distinguir mayúsculas) o cuyo
    /// teléfono contenga los dígitos del texto. Orden: nombre y luego Id.
    /// </summary>
    Task<PagedResult<Cliente>> BuscarAsync(string texto, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Deja el cliente en seguimiento; no guarda hasta llamar a <see cref="GuardarCambiosAsync"/>.</summary>
    void Agregar(Cliente cliente);

    /// <summary>
    /// Guarda los cambios pendientes de Cliente. Si otra solicitud creó, al mismo tiempo, un
    /// cliente con el mismo teléfono, lanza <see cref="GIRA.Domain.Exceptions.Reservas.ClienteDuplicadoException"/>
    /// en vez de dejar escapar el error de la base de datos.
    /// </summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
