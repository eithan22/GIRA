using GIRA.Application.Common;
using GIRA.Domain.Entities.Reservas;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.Interfaces.Repository.Reservas;

public interface IMesaGestionRepository
{
    Task<Mesa?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>True si ya hay una mesa con ese número en todo el restaurante (excluye la mesa indicada).</summary>
    Task<bool> ExisteNumeroAsync(int numero, Guid? excluirId, CancellationToken cancellationToken);
    Task<PagedResult<Mesa>> ListarAsync(Guid? zonaId, bool? activa, int page, int pageSize, CancellationToken cancellationToken);
    void Agregar(Mesa mesa);
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}