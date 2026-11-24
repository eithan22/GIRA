using GIRA.Domain.Entities.Personal;

namespace GIRA.Application.Interfaces.Repository.Personal;

/// <summary>Registro de los eventos de predicción que Personal rechazó, para revisión (esquema <c>personal</c>, GI-303).</summary>
public interface IEventoPrediccionDescartadoRepository
{
    /// <summary>Agrega un evento descartado al contexto; se guarda con <see cref="GuardarCambiosAsync"/>.</summary>
    void Agregar(EventoPrediccionDescartado evento);

    /// <summary>Guarda los cambios pendientes.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
