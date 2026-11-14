import { formatearFechaLarga, formatearHora } from '../reservasFormato.js';
import { textoLimiteCancelacion } from '../reservasTextos.js';
import { usePoliticasReserva } from '../hooks/usePoliticasReserva.js';

/**
 * Resumen tipo ticket del aside oscuro de /reservar (Main/Paso2.dc.html). Es el
 * <aside> completo (incluye reservas-tarjeta-ticket): las páginas lo colocan
 * directo como hijo de .reservas-tarjeta, junto a .reservas-tarjeta-principal.
 */
export default function TicketResumen({ fecha, franja, personas, onCambiar }) {
  const { politicas } = usePoliticasReserva();
  const hayFranja = Boolean(franja);

  return (
    <aside className="reservas-tarjeta-ticket">
      <div className="reservas-ticket-decoracion" aria-hidden="true" />

      <div className="reservas-ticket-cabecera">
        <span className="reservas-ticket-eyebrow">TU RESERVA</span>
        {onCambiar && (
          <button type="button" className="reservas-ticket-cambiar" onClick={onCambiar}>
            Cambiar
          </button>
        )}
      </div>

      <h2 className="reservas-ticket-titulo">{fecha ? formatearFechaLarga(fecha) : '—'}</h2>

      <dl className="reservas-ticket-lista">
        <div className="reservas-ticket-item">
          <span className="reservas-ticket-item-icono" aria-hidden="true">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#d7d0ff" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="9" cy="8" r="3.5" />
              <path d="M2.5 20c.6-3.6 3.2-5.5 6.5-5.5s5.9 1.9 6.5 5.5M16 4.5a3.5 3.5 0 0 1 0 7M18 14.8c2 .6 3.3 2.3 3.5 5.2" />
            </svg>
          </span>
          <div>
            <dt className="reservas-ticket-item-etiqueta">Personas</dt>
            <dd className="reservas-ticket-item-valor">{personas === 1 ? '1 persona' : `${personas} personas`}</dd>
          </div>
        </div>

        <div className="reservas-ticket-item">
          <span className="reservas-ticket-item-icono" aria-hidden="true">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#d7d0ff" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="9" />
              <path d="M12 7v5l3 2" />
            </svg>
          </span>
          <div>
            <dt className="reservas-ticket-item-etiqueta">Franja</dt>
            <dd className={`reservas-ticket-item-valor${hayFranja ? '' : ' reservas-ticket-item-valor--pendiente'}`}>
              {hayFranja ? `${franja.nombre} · ${formatearHora(franja.horaInicio)} – ${formatearHora(franja.horaFin)}` : '—'}
            </dd>
          </div>
        </div>
      </dl>

      <div className="reservas-ticket-perforado" aria-hidden="true">
        <span className="reservas-ticket-perforado-linea" />
      </div>

      <ul className="reservas-ticket-notas">
        <li className="reservas-ticket-nota">
          <svg className="reservas-ticket-nota-icono" aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#5eead4" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
            <path d="M5 12.5l4.5 4.5L19 7.5" />
          </svg>
          La mesa se asigna sola al confirmar.
        </li>
        <li className="reservas-ticket-nota">
          <svg className="reservas-ticket-nota-icono" aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#5eead4" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
            <path d="M5 12.5l4.5 4.5L19 7.5" />
          </svg>
          Hasta 2 reservas vigentes por teléfono.
        </li>
        <li className="reservas-ticket-nota">
          <svg className="reservas-ticket-nota-icono" aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#5eead4" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
            <path d="M5 12.5l4.5 4.5L19 7.5" />
          </svg>
          {textoLimiteCancelacion(politicas)}
        </li>
      </ul>
    </aside>
  );
}
