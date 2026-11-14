// Extracción pura del mensaje de error del servidor. Vive aparte de reservasService.js (que lee
// import.meta.env y crea el cliente HTTP) para poder probarla con `node --test`.

const CODIGOS_CON_DETALLE_VISIBLE = [400, 404, 409, 429];

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
