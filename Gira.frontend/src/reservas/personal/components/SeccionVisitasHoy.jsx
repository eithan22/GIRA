import {
  AYUDA_VISITAS_SIN_MARCA,
  TEXTO_REINTENTAR,
  TEXTO_VISITAS_CARGANDO,
  TEXTO_VISITAS_ERROR,
  TEXTO_VISITAS_VACIO,
  TITULO_VISITAS_HOY,
} from '../reservasPersonalTextos.js';
import { descripcionVisita, textoResumenVisitas, textoVisitasTruncadas } from '../reservasPersonalFormato.js';

// Texto de la región en vivo según el estado de la carga.
function textoDeEstado({ fase, datos, error }) {
  if (fase === 'cargando') return TEXTO_VISITAS_CARGANDO;
  if (fase === 'error') return error || TEXTO_VISITAS_ERROR;
  if (!datos || datos.totalVisitas === 0) return TEXTO_VISITAS_VACIO;
  return textoResumenVisitas(datos.totalVisitas, datos.totalPersonas);
}

/**
 * Sección «Visitas sin reserva de hoy» de la Agenda (GI-402): resumen (visitas y personas del día), la lista de las
 * más recientes y el aviso de que estas visitas no marcan la mesa como ocupada. La región `aria-live` está siempre en
 * el DOM para que los lectores de pantalla anuncien la carga, el resumen, el vacío o el error sin mover el foco. Si
 * falla, «Reintentar» vuelve a pedirla. No lleva datos personales: solo hora, personas, franja y mesa.
 */
export default function SeccionVisitasHoy({ fase, recargando, datos, error, onReintentar }) {
  const visitas = datos?.visitas ?? [];
  const hayLista = fase !== 'error' && visitas.length > 0;

  return (
    <section
      className="reservas-personal-tarjeta reservas-personal-visitas"
      aria-labelledby="agenda-visitas-hoy"
      aria-busy={recargando ? 'true' : 'false'}
    >
      <h2 id="agenda-visitas-hoy" className="reservas-personal-subtitulo">{TITULO_VISITAS_HOY}</h2>

      <p
        className={`reservas-personal-conteo${fase === 'error' ? ' reservas-campo-ayuda--error' : ''}`}
        role="status"
        aria-live="polite"
        aria-atomic="true"
      >
        {textoDeEstado({ fase, datos, error })}
      </p>

      {fase === 'error' && (
        <button
          type="button"
          className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
          onClick={onReintentar}
          disabled={recargando}
        >{TEXTO_REINTENTAR}</button>
      )}

      {hayLista && (
        <>
          <ul className="reservas-personal-visitas-lista">
            {visitas.map((visita) => (
              <li key={visita.id}>{descripcionVisita(visita)}</li>
            ))}
          </ul>
          {datos.truncada && (
            <p className="reservas-campo-ayuda">{textoVisitasTruncadas(visitas.length, datos.totalVisitas)}</p>
          )}
        </>
      )}

      <p className="reservas-campo-ayuda">{AYUDA_VISITAS_SIN_MARCA}</p>
    </section>
  );
}
