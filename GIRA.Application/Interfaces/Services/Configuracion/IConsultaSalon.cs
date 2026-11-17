namespace GIRA.Application.Interfaces.Services.Configuracion;

/// <summary>Franja activa tal como la ve Configuración.</summary>
public sealed record FranjaActivaInfo(Guid Id, string Nombre, TimeOnly HoraInicio, TimeOnly HoraFin);

/// <summary>
/// Puerto de Configuración hacia el salón y las reservas: lo que Configuración necesita saber para validar y
/// advertir sin leer las entidades de Reservas. Lo implementa el módulo Reservas.
/// </summary>
public interface IConsultaSalon
{
    /// <summary>Franjas activas, por hora de inicio.</summary>
    Task<IReadOnlyList<FranjaActivaInfo>> ListarFranjasActivasAsync(CancellationToken cancellationToken);

    /// <summary>Capacidad de la mesa activa más grande; 0 si no hay mesas activas.</summary>
    Task<int> CapacidadMaximaMesaActivaAsync(CancellationToken cancellationToken);

    /// <summary>Cantidad de reservas vigentes (Confirmada o EnCurso) en una fecha.</summary>
    Task<int> ContarReservasVigentesAsync(DateOnly fecha, CancellationToken cancellationToken);

    /// <summary>
    /// Cantidad de reservas vigentes de hoy en adelante (<paramref name="desde"/>) cuya fecha cae en ese día de la semana.
    /// </summary>
    Task<int> ContarReservasVigentesPorDiaSemanaAsync(DayOfWeek dia, DateOnly desde, CancellationToken cancellationToken);
}
