import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { formatearFechaCorta, formatearHora, formatearCodigoVisual, primerNombre } from '../reservasFormato.js';
import { AVISO_SIN_CORREO_TITULO, AVISO_SIN_CORREO_CUERPO, TEXTO_LIMITE_CANCELACION } from '../reservasTextos.js';
import Aviso from './Aviso.jsx';
import PortadaReservas from './PortadaReservas.jsx';

const PUNTOS_CONFETI = [
  { left: '12%', top: '130px', width: '10px', height: '10px', borderRadius: '3px', background: '#5eead4', transform: 'rotate(25deg)' },
  { left: '22%', top: '60px', width: '8px', height: '8px', borderRadius: '50%', background: '#b3a7ff' },
  { left: '34%', top: '150px', width: '12px', height: '6px', borderRadius: '3px', background: '#ffd37a', transform: 'rotate(-20deg)' },
  { right: '30%', top: '90px', width: '10px', height: '10px', borderRadius: '3px', background: '#b3a7ff', transform: 'rotate(40deg)' },
  { right: '16%', top: '150px', width: '8px', height: '8px', borderRadius: '50%', background: '#5eead4' },
  { right: '10%', top: '70px', width: '12px', height: '6px', borderRadius: '3px', background: '#ffd37a', transform: 'rotate(15deg)' },
];

/** Paso 3 del wizard de /reservar: confirmación con el código recién creado. */
export default function ReservaConfirmada({ reserva, telefono, onNueva }) {
  const navigate = useNavigate();
  const [copiado, setCopiado] = useState(false);
  const codigoRef = useRef(null);
  const tituloRef = useRef(null);
  const copiadoTimerRef = useRef(null);

  // Esta fase no la ve Reservar.jsx (que enfoca el h1 de PortadaReservas en
  // 'fecha'/'datos'): aquí el h1 vive dentro de este componente, así que se
  // enfoca solo al montarse.
  useEffect(() => {
    tituloRef.current?.focus();
  }, []);

  useEffect(() => () => {
    if (copiadoTimerRef.current) clearTimeout(copiadoTimerRef.current);
  }, []);

  async function copiarCodigo() {
    try {
      await navigator.clipboard.writeText(reserva.codigo);
      setCopiado(true);
      if (copiadoTimerRef.current) clearTimeout(copiadoTimerRef.current);
      copiadoTimerRef.current = setTimeout(() => setCopiado(false), 2000);
    } catch {
      // Sin Clipboard API (navegador viejo o sin permiso): selecciona el texto
      // para que el cliente pueda copiarlo a mano con Ctrl/Cmd+C.
      const nodo = codigoRef.current;
      if (nodo && window.getSelection) {
        const seleccion = window.getSelection();
        const rango = document.createRange();
        rango.selectNodeContents(nodo);
        seleccion.removeAllRanges();
        seleccion.addRange(rango);
      }
    }
  }

  function verMiReserva() {
    navigate('/mi-reserva', { state: { codigo: reserva.codigo, telefono } });
  }

  return (
    <>
      <PortadaReservas activa="reservar" corta>
        {PUNTOS_CONFETI.map((estilo, indice) => (
          // eslint-disable-next-line react/no-array-index-key
          <span key={indice} className="reservas-confeti" aria-hidden="true" style={estilo} />
        ))}

        <div className="reservas-exito-icono" aria-hidden="true">
          <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
            <path d="M5 12.5l4.5 4.5L19 7.5" />
          </svg>
        </div>
        <span className="reservas-hero-eyebrow reservas-hero-eyebrow--exito">RESERVA CONFIRMADA</span>
        <h1 ref={tituloRef} tabIndex={-1} className="reservas-hero-titulo reservas-hero-titulo--exito">
          ¡Listo, {primerNombre(reserva.nombreCliente)}! Tu mesa te espera
        </h1>
      </PortadaReservas>

      <section className="reservas-tarjeta reservas-tarjeta--angosta reservas-tarjeta--sola">
        <div className="reservas-codigo-panel">
          <span id="reservas-lbl-codigo" className="reservas-codigo-etiqueta">TU CÓDIGO DE RESERVA</span>
          <output ref={codigoRef} aria-labelledby="reservas-lbl-codigo" className="reservas-codigo-valor">
            {formatearCodigoVisual(reserva.codigo)}
          </output>
          <button type="button" className="reservas-codigo-copiar" onClick={copiarCodigo} aria-live="polite">
            <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="9" y="9" width="11" height="11" rx="2" />
              <path d="M5 15V6a2 2 0 0 1 2-2h9" />
            </svg>
            {copiado ? '¡Copiado!' : 'Copiar código'}
          </button>
        </div>

        <div className="reservas-codigo-perforado" aria-hidden="true">
          <span className="reservas-codigo-perforado-linea" />
        </div>

        <div className="reservas-confirmacion-cuerpo">
          <Aviso tipo="advertencia" titulo={AVISO_SIN_CORREO_TITULO}>{AVISO_SIN_CORREO_CUERPO}</Aviso>

          <dl className="reservas-detalle-grid">
            <div className="reservas-detalle-item">
              <span className="reservas-detalle-icono" aria-hidden="true">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="5" width="18" height="16" rx="2" />
                  <path d="M3 10h18M8 3v4M16 3v4" />
                </svg>
              </span>
              <div>
                <dt className="reservas-detalle-etiqueta">Fecha</dt>
                <dd className="reservas-detalle-valor">{formatearFechaCorta(reserva.fecha)}</dd>
              </div>
            </div>
            <div className="reservas-detalle-item">
              <span className="reservas-detalle-icono" aria-hidden="true">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="12" r="9" />
                  <path d="M12 7v5l3 2" />
                </svg>
              </span>
              <div>
                <dt className="reservas-detalle-etiqueta">Franja</dt>
                <dd className="reservas-detalle-valor">
                  {reserva.franjaNombre} · {formatearHora(reserva.franjaHoraInicio)} – {formatearHora(reserva.franjaHoraFin)}
                </dd>
              </div>
            </div>
            <div className="reservas-detalle-item">
              <span className="reservas-detalle-icono" aria-hidden="true">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="9" cy="8" r="3.5" />
                  <path d="M2.5 20c.6-3.6 3.2-5.5 6.5-5.5s5.9 1.9 6.5 5.5M16 4.5a3.5 3.5 0 0 1 0 7M18 14.8c2 .6 3.3 2.3 3.5 5.2" />
                </svg>
              </span>
              <div>
                <dt className="reservas-detalle-etiqueta">Personas</dt>
                <dd className="reservas-detalle-valor">{reserva.personas === 1 ? '1 persona' : `${reserva.personas} personas`}</dd>
              </div>
            </div>
            <div className="reservas-detalle-item">
              <span className="reservas-detalle-icono" aria-hidden="true">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="8" r="4" />
                  <path d="M4 21c.8-4 4-6 8-6s7.2 2 8 6" />
                </svg>
              </span>
              <div>
                <dt className="reservas-detalle-etiqueta">A nombre de</dt>
                <dd className="reservas-detalle-valor">{reserva.nombreCliente}</dd>
              </div>
            </div>
          </dl>

          <div className="reservas-confirmacion-acciones">
            <button type="button" className="reservas-boton reservas-boton--primario" onClick={verMiReserva}>Ver mi reserva</button>
            <button type="button" className="reservas-boton reservas-boton--secundario" onClick={onNueva}>Hacer otra reserva</button>
          </div>
        </div>
      </section>

      <p className="reservas-pie-nota">{TEXTO_LIMITE_CANCELACION}</p>
    </>
  );
}
