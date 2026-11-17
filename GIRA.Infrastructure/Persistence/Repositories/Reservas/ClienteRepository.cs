using GIRA.Application.Common;
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
    public async Task<PagedResult<Cliente>> BuscarAsync(string texto, int page, int pageSize, CancellationToken cancellationToken)
    {
        var buscado = texto.Trim().ToLower();
        var digitos = new string(buscado.Where(char.IsDigit).ToArray());
        var buscaTelefono = digitos.Length > 0;

        var consulta = _contexto.Clientes.Where(c =>
            c.Nombre.ToLower().Contains(buscado) || (buscaTelefono && c.Telefono.Contains(digitos)));

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderBy(c => c.Nombre).ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Cliente>(items, total, page, pageSize);
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
