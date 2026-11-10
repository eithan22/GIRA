import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { reservasService, extraerMensajeError } from '../reservasService.js';
import { formatearFechaCorta, formatearHora, formatearCodigoVisual, primerNombre } from '../reservasFormato.js';
import {
  ESTADOS_RESERVA,
  ESTADO_DESCONOCIDO,
  MENSAJE_ERROR_GENERICO,
  TEXTO_LIMITE_CANCELACION,
  TEXTO_RESERVA_CANCELADA,
  TEXTO_DIALOGO_CANCELAR_TITULO,
  TEXTO_DIALOGO_CANCELAR_CUERPO,
} from '../reservasTextos.js';
import Aviso from './Aviso.jsx';
import PortadaReservas from './PortadaReservas.jsx';

const DURACION_BLOQUEO_429_MS = 10_000;

// Los 3 textos de 409 que puede devolver /codigo/{codigo}/cancelar: se
// distinguen por una sub-cadena del mensaje porque el servidor no manda un
// código de error propio, solo el detail en texto.
function tipoDe409(mensaje) {
  if (mensaje.includes('anticipación')) return '409-anticipacion'; // límite de 2h
  if (mensaje.includes('cambió mientras se procesaba')) return '409-concurrencia';
  return '409-ya-no-confirmada'; // "Esta reserva ya no se puede cancelar."
}

const TIPOS_409_CON_VOLVER_A_CONSULTAR = ['409-concurrencia', '409-ya-no-confirmada'];

/** Fase "detalle" de /mi-reserva: datos de la reserva y su cancelación. */
export default function DetalleReserva({ reserva, credenciales, onCancelada, onVolverAConsultar, onNueva }) {
  const navigate = useNavigate();
  const [estadoLocal, setEstadoLocal] = useState(null);
  const [confirmando, setConfirmando] = useState(false);
  const [cancelando, setCancelando] = useState(false);
  const [siempreFallara, setSiempreFallara] = useState(false);
  const [errorCancelar, setErrorCancelar] = useState(null);
  const [bloqueado429, setBloqueado429] = useState(false);
  const [recargando, setRecargando] = useState(false);

  const envioEnCursoRef = useRef(false);
  const bloqueoTimerRef = useRef(null);
  const botonCancelarRef = useRef(null);
  const botonMantenerRef = useRef(null);
  const tituloRef = useRef(null);
  const avisoExitoRef = useRef(null);

  useEffect(() => () => {
    if (bloqueoTimerRef.current) clearTimeout(bloqueoTimerRef.current);
  }, []);

  // El h1 de esta fase vive aquí, no en MiReserva.jsx: se enfoca al montarse.
  useEffect(() => {
    tituloRef.current?.focus();
  }, []);

  // Tras cancelar con éxito, el foco pasa al propio aviso (tabIndex=-1): es
  // lo que realmente cambió en la pantalla, más útil que quedarse en el h1.
  useEffect(() => {
    if (estadoLocal === 'Cancelada') {
      avisoExitoRef.current?.focus();
    }
  }, [estadoLocal]);

  useEffect(() => {
    if (!confirmando) return;
    botonMantenerRef.current?.focus();

    function alTeclear(evento) {
      // Mientras se está cancelando, Escape se ignora: no hay nada seguro
      // que cerrar a mitad de una petición en curso.
      if (evento.key === 'Escape' && !cancelando) cerrarDialogo();
    }
    window.addEventListener('keydown', alTeclear);
    return () => window.removeEventListener('keydown', alTeclear);
  }, [confirmando, cancelando]);

  const estadoActual = estadoLocal ?? reserva.estado;
  const info = ESTADOS_RESERVA[estadoActual] ?? ESTADO_DESCONOCIDO;
  const etiqueta = ESTADOS_RESERVA[estadoActual]?.etiqueta ?? estadoActual;

  function abrirDialogo() {
    setErrorCancelar(null);
    setSiempreFallara(false);
    setConfirmando(true);
  }

  function cerrarDialogo() {
    if (cancelando) return;
    setConfirmando(false);
    setErrorCancelar(null);
    setSiempreFallara(false);
    botonCancelarRef.current?.focus();
  }

  async function confirmarCancelacion() {
    if (envioEnCursoRef.current) return;
    envioEnCursoRef.current = true;
    setCancelando(true);
    setErrorCancelar(null);

    try {
      await reservasService.cancelarPorCodigo(credenciales.codigo, credenciales.telefono);
      setEstadoLocal('Cancelada');
      setConfirmando(false);
      onCancelada?.();
    } catch (err) {
      const status = err.response?.status;
      const mensaje = extraerMensajeError(err, MENSAJE_ERROR_GENERICO);

      if (status === 429) {
        setErrorCancelar({ tipo: '429', mensaje });
        setBloqueado429(true);
        if (bloqueoTimerRef.current) clearTimeout(bloqueoTimerRef.current);
        bloqueoTimerRef.current = setTimeout(() => setBloqueado429(false), DURACION_BLOQUEO_429_MS);
      } else if (status === 409) {
        const tipo = tipoDe409(mensaje);
        setErrorCancelar({ tipo, mensaje });
        // Fuera del límite de anticipación: reintentar siempre va a volver a
        // fallar (el tiempo no retrocede), así que "Sí, cancelar" queda
        // deshabilitado hasta que se cierre y se vuelva a abrir el diálogo.
        if (tipo === '409-anticipacion') setSiempreFallara(true);
      } else {
        // 404 y red/500.
        setErrorCancelar({ tipo: 'otro', mensaje });
      }
    } finally {
      envioEnCursoRef.current = false;
      setCancelando(false);
    }
  }

  async function volverAConsultar() {
    setRecargando(true);
    try {
      await onVolverAConsultar?.();
      setErrorCancelar(null);
      setConfirmando(false);
    } catch (err) {
      setErrorCancelar({ tipo: 'otro', mensaje: extraerMensajeError(err, MENSAJE_ERROR_GENERICO) });
    } finally {
      setRecargando(false);
    }
  }

  return (
    <>
      <PortadaReservas activa="mi-reserva" corta>
        <button type="button" className="reservas-portada-volver" onClick={onNueva}>
          <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M19 12H5M11 6l-6 6 6 6" />
          </svg>
          Buscar otra reserva
        </button>

        <h1 ref={tituloRef} tabIndex={-1} className="reservas-hero-titulo reservas-hero-titulo--compacta">
          Tu reserva, {primerNombre(reserva.nombreCliente)}
        </h1>
      </PortadaReservas>

      <section className="reservas-tarjeta reservas-tarjeta--angosta reservas-tarjeta--sola reservas-tarjeta--detalle">
        {estadoLocal === 'Cancelada' && (
          <div ref={avisoExitoRef} tabIndex={-1} className="reservas-detalle-aviso-cancelada">
            <Aviso tipo="exito">{TEXTO_RESERVA_CANCELADA}</Aviso>
          </div>
        )}

        <div className="reservas-detalle-cabecera">
          <div>
            <span className="reservas-detalle-codigo-etiqueta">CÓDIGO DE RESERVA</span>
            <span className="reservas-detalle-codigo-valor">{formatearCodigoVisual(reserva.codigo)}</span>
          </div>
          <span className="reservas-estado-pill" style={{ background: info.fondo, color: info.texto, '--reservas-punto-color': info.punto }}>
            <span className="reservas-estado-punto" aria-hidden="true" />
            {etiqueta}
          </span>
        </div>

        <div className="reservas-codigo-perforado" aria-hidden="true">
          <span className="reservas-codigo-perforado-linea" />
        </div>

        <dl className="reservas-detalle-grid reservas-detalle-grid--acolchada">
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
        </dl>

        {info.puedeCancelar && !confirmando && (
          <div className="reservas-detalle-nota-cancelar">
            <span className="reservas-detalle-nota-cancelar-texto"><strong>{TEXTO_LIMITE_CANCELACION}</strong></span>
            <button ref={botonCancelarRef} type="button" className="reservas-boton reservas-boton--chico reservas-boton--fantasma-error" onClick={abrirDialogo}>
              Cancelar reserva
            </button>
          </div>
        )}

        {confirmando && (
          <div className="reservas-detalle-dialogo-envoltura">
            <div
              role="alertdialog"
              aria-labelledby="reservas-dialogo-titulo"
              aria-describedby="reservas-dialogo-texto"
              aria-busy={cancelando}
              className="reservas-alertdialog"
            >
              <h2 id="reservas-dialogo-titulo" className="reservas-alertdialog-titulo">{TEXTO_DIALOGO_CANCELAR_TITULO}</h2>
              <p id="reservas-dialogo-texto" className="reservas-alertdialog-texto">{TEXTO_DIALOGO_CANCELAR_CUERPO}</p>

              {errorCancelar && !TIPOS_409_CON_VOLVER_A_CONSULTAR.includes(errorCancelar.tipo) && (
                <Aviso tipo={errorCancelar.tipo === '429' ? 'advertencia' : 'error'} rol={errorCancelar.tipo === '429' ? 'alert' : undefined}>
                  {errorCancelar.mensaje}
                </Aviso>
              )}
              {errorCancelar && TIPOS_409_CON_VOLVER_A_CONSULTAR.includes(errorCancelar.tipo) && (
                <Aviso tipo="error">
                  {errorCancelar.mensaje}{' '}
                  <button type="button" className="reservas-boton reservas-boton--chico reservas-boton--secundario" onClick={volverAConsultar} disabled={recargando}>
                    {recargando ? 'Consultando…' : 'Volver a consultar'}
                  </button>
                </Aviso>
              )}

              <div className="reservas-dialogo-acciones">
                <button
                  type="button"
                  className="reservas-boton reservas-boton--chico reservas-boton--peligro"
                  onClick={confirmarCancelacion}
                  disabled={bloqueado429 || siempreFallara || cancelando}
                >
                  {cancelando ? 'Cancelando…' : 'Sí, cancelar reserva'}
                </button>
                <button
                  ref={botonMantenerRef}
                  type="button"
                  className="reservas-boton reservas-boton--chico reservas-boton--secundario"
                  onClick={cerrarDialogo}
                  disabled={cancelando}
                >
                  Mantener mi reserva
                </button>
              </div>
            </div>
          </div>
        )}

        {estadoLocal === 'Cancelada' && (
          <div className="reservas-detalle-accion-final">
            <button type="button" className="reservas-boton reservas-boton--primario" onClick={() => navigate('/reservar')}>
              Hacer una nueva reserva
            </button>
          </div>
        )}
      </section>
    </>
  );
}
