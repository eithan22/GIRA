// Extracción pura del mensaje de error del servidor. Vive aparte de reservasService.js (que lee
// import.meta.env y crea el cliente HTTP) para poder probarla con `node --test`.

const CODIGOS_CON_DETALLE_VISIBLE = [400, 404, 409, 429];

/**
 * Catálogo de códigos estables que el backend manda en `ProblemDetails.codigo` (ver
 * CodigosErrorReservas.cs). El frontend decide por este código, nunca por el texto de `detail`
 * (que puede cambiar de redacción o según un parámetro configurado, como el máximo de vigentes).
 */
export const CODIGOS_ERROR = {
  LIMITE_VIGENTES: 'LIMITE_VIGENTES',
  CANCELACION_FUERA_DE_PLAZO: 'CANCELACION_FUERA_DE_PLAZO',
  CAMBIO_CONCURRENTE: 'CAMBIO_CONCURRENTE',
  CONTACTAR_RESTAURANTE: 'CONTACTAR_RESTAURANTE',
  RESERVA_NO_CANCELABLE: 'RESERVA_NO_CANCELABLE',
  // ZONA_SIN_MESAS ya lo manda el backend (Fase 3); los dos de abajo están reservados para la Fase 4.
  ZONA_SIN_MESAS: 'ZONA_SIN_MESAS',
  LIMITE_IP_DIARIO: 'LIMITE_IP_DIARIO',
  TELEFONO_BLOQUEADO_NOSHOW: 'TELEFONO_BLOQUEADO_NOSHOW',
};

// Código estable del error, o null si el servidor no mandó ninguno (errores sin código asignado
// todavía, o cualquier error que no sea un ProblemDetails de este backend).
export function codigoDeError(error) {
  const codigo = error?.response?.data?.codigo;
  return typeof codigo === 'string' && codigo ? codigo : null;
}

// Devuelve el `detail` del servidor en 400/404/409/429; en cualquier otro caso
// (500, error de red, timeout) devuelve el texto genérico que pase el llamador,
// sin exponer nunca el mensaje crudo del error.
export function extraerMensajeError(error, mensajePorDefecto) {
  const status = error?.response?.status;
  const detail = error?.response?.data?.detail;
  if (CODIGOS_CON_DETALLE_VISIBLE.includes(status) && typeof detail === 'string' && detail.trim()) {
    return detail;
  }
  return mensajePorDefecto;
}

/**
 * Errores por campo de un `ValidationProblem` (400 con `errors`: { campo: [mensajes] }, campos en camelCase):
 * { campo: primerMensaje }. Devuelve {} si el error no es de ese tipo.
 */
export function errorPorCampo(error) {
  const errores = error?.response?.status === 400 ? error?.response?.data?.errors : null;
  if (!errores || typeof errores !== 'object') return {};
  return Object.fromEntries(
    Object.entries(errores)
      .map(([campo, mensajes]) => [campo, Array.isArray(mensajes) ? mensajes.find((m) => typeof m === 'string' && m.trim()) : mensajes])
      .filter(([, mensaje]) => typeof mensaje === 'string' && mensaje.trim()),
  );
}
