import { useEffect, useRef, useState } from 'react';
import { extraerMensajeError } from '../reservasService.js';
import {
  AYUDA_TELEFONO_CORTA,
  AYUDA_TELEFONO_FORMATO,
  AYUDA_TELEFONO_CODIGO_PAIS,
  AYUDA_CODIGO,
  MENSAJE_ERROR_GENERICO,
  TELEFONO_RESTAURANTE,
} from '../reservasTextos.js';
import Aviso from './Aviso.jsx';

const DURACION_BLOQUEO_429_MS = 10_000;
const PATRON_TELEFONO = /^(809|829|849)\d{7}$/;

// Mismo orden que PasoDatos.jsx: a) 11 dígitos con 1 inicial -> código de
// país; b) menos de 10 -> incompleto; c) 10 que no empiezan en 809/829/849 ->
// formato. Este endpoint no valida nada en el servidor (un dato mal escrito
// da 404 neutro), pero no tiene sentido dejar mandar un teléfono con el
// código de país pegado por accidente.
function validarTelefono(digitos) {
  if (!digitos) return '';
  if (digitos.length === 11 && digitos.startsWith('1')) return AYUDA_TELEFONO_CODIGO_PAIS;
  if (digitos.length < 10) return AYUDA_TELEFONO_CORTA;
  if (digitos.length === 10 && PATRON_TELEFONO.test(digitos)) return '';
  return AYUDA_TELEFONO_FORMATO;
}

/**
 * Fase "buscar" de /mi-reserva: formulario de código + teléfono.
 * `errorInicial`/`bloqueadoInicial` llegan de MiReserva.jsx cuando la consulta
 * automática (código llegado por location.state) falla: se aplican una sola
 * vez (yaAplicoErrorRef/yaAplicoBloqueoRef) y, de ahí en adelante, el 429 lo
 * maneja este propio componente con su temporizador — nunca se vuelve a
 * sincronizar desde el prop, para no pisar un bloqueo que el usuario se haya
 * ganado después con su propio envío.
 */
export default function BuscarReserva({ inicial, errorInicial, bloqueadoInicial, onBuscar, buscando }) {
  const [codigo, setCodigo] = useState(inicial?.codigo ?? '');
  const [telefono, setTelefono] = useState(inicial?.telefono ?? '');
  const [telefonoTocado, setTelefonoTocado] = useState(false);
  const [error, setError] = useState(errorInicial ? { tipo: bloqueadoInicial ? '429' : 'otro', mensaje: errorInicial } : null);
  const [bloqueado429, setBloqueado429] = useState(Boolean(bloqueadoInicial));
  const envioEnCursoRef = useRef(false);
  const bloqueoTimerRef = useRef(null);
  const yaAplicoErrorRef = useRef(Boolean(errorInicial));
  const yaAplicoBloqueoRef = useRef(Boolean(bloqueadoInicial));

  useEffect(() => () => {
    if (bloqueoTimerRef.current) clearTimeout(bloqueoTimerRef.current);
  }, []);

  useEffect(() => {
    if (errorInicial && !yaAplicoErrorRef.current) {
      yaAplicoErrorRef.current = true;
      setError({ tipo: bloqueadoInicial ? '429' : 'otro', mensaje: errorInicial });
    }
  }, [errorInicial, bloqueadoInicial]);

  useEffect(() => {
    if (bloqueadoInicial && !yaAplicoBloqueoRef.current) {
      yaAplicoBloqueoRef.current = true;
      setBloqueado429(true);
      if (bloqueoTimerRef.current) clearTimeout(bloqueoTimerRef.current);
      bloqueoTimerRef.current = setTimeout(() => setBloqueado429(false), DURACION_BLOQUEO_429_MS);
    }
  }, [bloqueadoInicial]);

  // aria-invalid solo cuando hay 10 u 11 dígitos inválidos, o tras blur —
  // nunca mientras el usuario todavía está escribiendo los primeros dígitos.
  const telefonoCompleto = telefono.length === 10 || telefono.length === 11;
  const errorTelefonoVisible = (telefonoTocado || telefonoCompleto) ? validarTelefono(telefono) : '';

  // "completo" (para habilitar el botón) siempre valida de verdad, sin
  // importar si ya se mostró el error o no: nunca se manda un teléfono que
  // no pase a)/b)/c).
  const completo = codigo.length === 8 && telefono.length > 0 && !validarTelefono(telefono);

  async function enviar(evento) {
    evento.preventDefault();
    if (envioEnCursoRef.current || !completo) return;

    envioEnCursoRef.current = true;
    setError(null);
    try {
      await onBuscar({ codigo, telefono });
    } catch (err) {
      const mensaje = extraerMensajeError(err, MENSAJE_ERROR_GENERICO);
      if (err.response?.status === 429) {
        setError({ tipo: '429', mensaje });
        setBloqueado429(true);
        if (bloqueoTimerRef.current) clearTimeout(bloqueoTimerRef.current);
        bloqueoTimerRef.current = setTimeout(() => setBloqueado429(false), DURACION_BLOQUEO_429_MS);
      } else {
        setError({ tipo: 'otro', mensaje });
      }
    } finally {
      envioEnCursoRef.current = false;
    }
  }

  return (
    <div className="reservas-tarjeta">
      <section className="reservas-tarjeta-principal">
        <p className="reservas-nota reservas-nota--primera">
          Escribe el código de 8 dígitos que recibiste al reservar y el teléfono con el que reservaste.
        </p>

        <form onSubmit={enviar}>
          <div className="reservas-campo reservas-campo--separado">
            <label className="reservas-campo-etiqueta" htmlFor="reservas-buscar-codigo">Código de reserva</label>
            <input
              id="reservas-buscar-codigo"
              type="text"
              inputMode="numeric"
              autoComplete="off"
              maxLength={8}
              placeholder="12345678"
              className="reservas-input reservas-input--codigo"
              value={codigo}
              aria-describedby="reservas-buscar-ayuda-codigo"
              onChange={(evento) => setCodigo(evento.target.value.replace(/\D/g, '').slice(0, 8))}
            />
            <span id="reservas-buscar-ayuda-codigo" className="reservas-campo-ayuda">{AYUDA_CODIGO}</span>
          </div>

          <div className="reservas-campo reservas-campo--separado">
            <label className="reservas-campo-etiqueta" htmlFor="reservas-buscar-telefono">Teléfono</label>
            <div className="reservas-campo-control">
              <span className="reservas-campo-icono" aria-hidden="true">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="7" y="2.5" width="10" height="19" rx="2.5" />
                  <path d="M11 18.5h2" />
                </svg>
              </span>
              <input
                id="reservas-buscar-telefono"
                type="tel"
                inputMode="numeric"
                autoComplete="tel-national"
                className="reservas-input reservas-input--con-icono"
                value={telefono}
                aria-invalid={Boolean(errorTelefonoVisible)}
                aria-describedby="reservas-buscar-ayuda-telefono"
                onChange={(evento) => setTelefono(evento.target.value.replace(/\D/g, '').slice(0, 11))}
                onBlur={() => setTelefonoTocado(true)}
              />
            </div>
            <span
              id="reservas-buscar-ayuda-telefono"
              className={`reservas-campo-ayuda${errorTelefonoVisible ? ' reservas-campo-ayuda--error' : ''}`}
              aria-live="polite"
            >
              {errorTelefonoVisible || AYUDA_TELEFONO_CORTA}
            </span>
          </div>

          {error && (
            <Aviso tipo={error.tipo === '429' ? 'advertencia' : 'error'} rol={error.tipo === '429' ? 'alert' : undefined}>
              {error.mensaje}
            </Aviso>
          )}

          <button
            type="submit"
            className="reservas-boton reservas-boton--primario reservas-boton--ancho"
            disabled={!completo || buscando || bloqueado429}
          >
            {buscando ? 'Buscando…' : (
              <>
                Buscar mi reserva
                <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="11" cy="11" r="7" />
                  <path d="M20 20l-3.5-3.5" />
                </svg>
              </>
            )}
          </button>
        </form>

        <p className="reservas-nota reservas-nota--chica">¿Perdiste tu código? Llama al restaurante: <strong>{TELEFONO_RESTAURANTE}</strong>.</p>
      </section>

      <aside className="reservas-tarjeta-ticket">
        <div className="reservas-ticket-decoracion" aria-hidden="true" />
        <span className="reservas-ticket-eyebrow">CÓMO FUNCIONA</span>
        <h2 className="reservas-ticket-titulo">Tu código es tu llave</h2>

        <ol className="reservas-ticket-pasos">
          <li className="reservas-ticket-paso">
            <span className="reservas-ticket-paso-numero" aria-hidden="true">1</span>
            <span className="reservas-ticket-paso-texto">
              <strong className="reservas-ticket-paso-titulo">Escribe tus datos</strong>
              El código de 8 dígitos y el teléfono de tu reserva.
            </span>
          </li>
          <li className="reservas-ticket-paso">
            <span className="reservas-ticket-paso-numero" aria-hidden="true">2</span>
            <span className="reservas-ticket-paso-texto">
              <strong className="reservas-ticket-paso-titulo">Revisa tu reserva</strong>
              Fecha, franja, personas y estado.
            </span>
          </li>
          <li className="reservas-ticket-paso">
            <span className="reservas-ticket-paso-numero" aria-hidden="true">3</span>
            <span className="reservas-ticket-paso-texto">
              <strong className="reservas-ticket-paso-titulo">Cancela si lo necesitas</strong>
              Hasta 2 horas antes de la franja.
            </span>
          </li>
        </ol>

        <div className="reservas-ticket-nota-info">
          <svg className="reservas-ticket-nota-icono" aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#5eead4" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <rect x="4" y="11" width="16" height="9" rx="2" />
            <path d="M8 11V8a4 4 0 0 1 8 0v3" />
          </svg>
          Por seguridad, si algún dato no coincide no te diremos cuál de los dos falló.
        </div>
      </aside>
    </div>
  );
}
