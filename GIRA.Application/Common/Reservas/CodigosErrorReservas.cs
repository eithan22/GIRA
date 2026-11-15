namespace GIRA.Application.Common.Reservas;

/// <summary>
/// Códigos estables para los errores de Reservas que el cliente necesita distinguir sin leer el texto de
/// <see cref="OperationResult.Error"/> (que puede cambiar de redacción, de idioma o según el parámetro
/// configurado, como el máximo de reservas vigentes). El código va en la extensión "codigo" del
/// <c>ProblemDetails</c> (ver <c>GIRA.Api.Common.OperationResultExtensions</c>); el mensaje en español se
/// conserva igual, para mostrarlo al usuario. Un error sin código asignado sigue funcionando igual que antes.
/// </summary>
public static class CodigosErrorReservas
{
    /// <summary>El teléfono ya alcanzó el máximo de reservas vigentes configurado.</summary>
    public const string LimiteVigentes = "LIMITE_VIGENTES";

    /// <summary>Se intentó cancelar fuera del límite de cancelación (antelación o anticipación, según el canal).</summary>
    public const string CancelacionFueraDePlazo = "CANCELACION_FUERA_DE_PLAZO";

    /// <summary>Otra solicitud modificó la misma reserva antes de que esta terminara de procesarse.</summary>
    public const string CambioConcurrente = "CAMBIO_CONCURRENTE";

    /// <summary>El caso no se resuelve por la web; el cliente debe llamar al restaurante.</summary>
    public const string ContactarRestaurante = "CONTACTAR_RESTAURANTE";

    /// <summary>La reserva ya no está Confirmada (está Cancelada, NoAsistio, EnCurso o Completada): no se puede cancelar.</summary>
    public const string ReservaNoCancelable = "RESERVA_NO_CANCELABLE";

    /// <summary>El canal del personal envió ZonaId: ese canal no usa zona, la mesa se asigna a mano.</summary>
    public const string ZonaNoAplicaPersonal = "ZONA_NO_APLICA_PERSONAL";

    // --- Reservados para las Fases 3 y 4: declarados ya para fijar el nombre final, sin usar todavía. ---

    /// <summary>Fase 3: la zona elegida ya no tiene mesas disponibles en esa franja.</summary>
    public const string ZonaSinMesas = "ZONA_SIN_MESAS";

    /// <summary>Fase 4: esa IP alcanzó el máximo de reservas web del día de negocio actual.</summary>
    public const string LimiteIpDiario = "LIMITE_IP_DIARIO";

    /// <summary>Fase 4: ese teléfono tiene inasistencias recientes y el canal web no lo acepta.</summary>
    public const string TelefonoBloqueadoNoShow = "TELEFONO_BLOQUEADO_NOSHOW";
}
