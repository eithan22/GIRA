using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Exceptions.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Reservas;

/// <summary>Implementación de <see cref="IClienteRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class ClienteRepository : IClienteRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public ClienteRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public Task<Cliente?> BuscarPorTelefonoAsync(string telefono, CancellationToken cancellationToken)
    {
        return _contexto.Clientes.FirstOrDefaultAsync(c => c.Telefono == telefono, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Cliente?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _contexto.Clientes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public void Agregar(Cliente cliente)
    {
        _contexto.Clientes.Add(cliente);
    }

    /// <inheritdoc />
    public async Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ViolacionesIndiceUnico.Es(ex, "IX_Cliente_Telefono"))
        {
            // El Cliente que intentamos insertar queda "Added" en el rastreador aunque la base
            // lo rechazó; si no lo destrabamos, el próximo SaveChanges lo volvería a intentar
            // insertar y fallaría otra vez por el mismo teléfono.
            foreach (var entrada in _contexto.ChangeTracker.Entries<Cliente>()
                .Where(e => e.State == EntityState.Added)
                .ToList())
            {
                entrada.State = EntityState.Detached;
            }

            throw new ClienteDuplicadoException(
                "Otra solicitud ya creó un cliente con este teléfono.", ex);
        }
    }
}
