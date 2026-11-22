using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Application.Interfaces.Repository.Personal;

/// <summary>Acceso a las sugerencias de personal (esquema <c>personal</c>, SRS 5.4).</summary>
public interface ISugerenciaTurnoRepository
{
    /// <summary>Busca una sugerencia por identificador, con seguimiento de cambios; null si no existe.</summary>
    Task<SugerenciaTurno?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lista las sugerencias, opcionalmente filtradas por estado, sin seguimiento de cambios.</summary>
    Task<IReadOnlyList<SugerenciaTurno>> ListarAsync(EstadoSugerencia? estado, CancellationToken cancellationToken);

    /// <summary>Lista las sugerencias pendientes entre dos fechas (inclusive), con seguimiento de cambios para poder vencerlas.</summary>
    Task<IReadOnlyList<SugerenciaTurno>> ListarPendientesEntreAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);

    /// <summary>Agrega una sugerencia nueva al contexto; se guarda con <see cref="GuardarCambiosAsync"/>.</summary>
    void Agregar(SugerenciaTurno sugerencia);

    /// <summary>Guarda los cambios pendientes.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
