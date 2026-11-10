import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { extraerMensajeError } from '../reservasService.js';
import {
  AYUDA_TELEFONO_FORMATO,
  AYUDA_TELEFONO_CORTA,
  AYUDA_TELEFONO_CODIGO_PAIS,
  MENSAJE_NOMBRE_OBLIGATORIO,
  MENSAJE_NOMBRE_MUY_LARGO,
  MENSAJE_TELEFONO_OBLIGATORIO,
  MENSAJE_ERROR_GENERICO,
} from '../reservasTextos.js';
import Aviso from './Aviso.jsx';

// Mismos límites que CrearReservaRequestValidator.cs / TelefonoRd.cs en el
// backend: nombre hasta 150 caracteres, teléfono de 10 dígitos que empiece con
// 809/829/849. El input deja escribir (o pegar) hasta 11 dígitos — nunca 10 —
// para no recortar en silencio un "+1 809 555 1234" pegado y mandar otro
// número sin que el cliente se dé cuenta; el propio validarTelefono es quien
// detecta el caso de 11 dígitos y pide corregirlo, sin tocar lo que escribió.
const MAX_LARGO_NOMBRE = 150;
const PATRON_TELEFONO = /^(809|829|849)\d{7}$/;
const DURACION_BLOQUEO_429_MS = 10_000;

function validarNombre(nombre) {
  if (!nombre.trim()) return MENSAJE_NOMBRE_OBLIGATORIO;
  if (nombre.length > MAX_LARGO_NOMBRE) return MENSAJE_NOMBRE_MUY_LARGO;
  return '';
}

// Orden exacto pedido: a) 11 dígitos con 1 inicial -> código de país;
// b) menos de 10 -> incompleto; c) 10 que no empiezan en 809/829/849 -> formato.
function validarTelefono(digitos) {
  if (!digitos) return MENSAJE_TELEFONO_OBLIGATORIO;
  if (digitos.length === 11 && digitos.startsWith('1')) return AYUDA_TELEFONO_CODIGO_PAIS;
  if (digitos.length < 10) return AYUDA_TELEFONO_CORTA;
  if (digitos.length === 10 && PATRON_TELEFONO.test(digitos)) return '';
  return AYUDA_TELEFONO_FORMATO;
}

/** Paso 2 del wizard de /reservar: nombre y teléfono, y el envío final. */
export default function PasoDatos({ valor, onCambiar, onAtras, onConfirmar, enviando }) {
  const { nombre, telefono } = valor;
  const [tocado, setTocado] = useState({ nombre: false, telefono: false });
  const [enviandoLocal, setEnviandoLocal] = useState(false);
  const [errorEnvio, setErrorEnvio] = useState(null);
  const [bloqueado429, setBloqueado429] = useState(false);
  const envioEnCursoRef = useRef(false);
  const bloqueoTimerRef = useRef(null);

  useEffect(() => () => {
    if (bloqueoTimerRef.current) clearTimeout(bloqueoTimerRef.current);
  }, []);

  const errorNombre = tocado.nombre ? validarNombre(nombre) : '';
  const errorTelefono = tocado.telefono ? validarTelefono(telefono) : '';
  const enviandoAhora = enviandoLocal || Boolean(enviando);

  async function enviar(evento) {
    evento.preventDefault();
    if (envioEnCursoRef.current) return;

    setTocado({ nombre: true, telefono: true });
    const mensajeNombre = validarNombre(nombre);
    const mensajeTelefono = validarTelefono(telefono);
    if (mensajeNombre || mensajeTelefono) return;

    envioEnCursoRef.current = true;
    setEnviandoLocal(true);
    setErrorEnvio(null);

    try {
      await onConfirmar({ nombre: nombre.trim(), telefono });
    } catch (err) {
      const status = err.response?.status;

      if (status === 429) {
        setErrorEnvio({ tipo: '429', mensaje: extraerMensajeError(err, MENSAJE_ERROR_GENERICO) });
        setBloqueado429(true);
        if (bloqueoTimerRef.current) clearTimeout(bloqueoTimerRef.current);
        bloqueoTimerRef.current = setTimeout(() => setBloqueado429(false), DURACION_BLOQUEO_429_MS);
      } else if (status === 409) {
        const mensaje = extraerMensajeError(err, MENSAJE_ERROR_GENERICO);
        setErrorEnvio({
          tipo: mensaje.includes('2 reservas vigentes') ? '409-limite' : '409-mesa',
          mensaje,
        });
      } else if (status === 400) {
        setErrorEnvio({ tipo: '400', mensaje: extraerMensajeError(err, MENSAJE_ERROR_GENERICO) });
      } else {
        setErrorEnvio({ tipo: 'generico', mensaje: MENSAJE_ERROR_GENERICO });
      }
    } finally {
      envioEnCursoRef.current = false;
      setEnviandoLocal(false);
    }
  }

  return (
    <section>
      <ol className="reservas-pasos">
        <li className="reservas-paso">
          <span className="reservas-paso-numero reservas-paso-numero--hecho" aria-hidden="true">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
              <path d="M5 12.5l4.5 4.5L19 7.5" />
            </svg>
          </span>
          <span>Fecha y franja</span>
        </li>
        <li className="reservas-paso-separador reservas-paso-separador--hecho" aria-hidden="true" />
        <li className="reservas-paso reservas-paso--activo" aria-current="step">
          <span className="reservas-paso-numero reservas-paso-numero--activo">2</span>
          <strong>Tus datos</strong>
        </li>
      </ol>

      <form onSubmit={enviar}>
        <div className="reservas-campo reservas-campo--separado">
          <label className="reservas-campo-etiqueta" htmlFor="reservas-nombre">Nombre completo</label>
          <div className="reservas-campo-control">
            <span className="reservas-campo-icono" aria-hidden="true">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="8" r="4" />
                <path d="M4 21c.8-4 4-6 8-6s7.2 2 8 6" />
              </svg>
            </span>
            <input
              id="reservas-nombre"
              type="text"
              autoComplete="name"
              maxLength={MAX_LARGO_NOMBRE}
              className="reservas-input reservas-input--con-icono"
              value={nombre}
              aria-invalid={Boolean(errorNombre)}
              aria-describedby="reservas-ayuda-nombre"
              onChange={(evento) => onCambiar({ nombre: evento.target.value })}
              onBlur={() => setTocado((t) => ({ ...t, nombre: true }))}
            />
          </div>
          <span
            id="reservas-ayuda-nombre"
            className={`reservas-campo-ayuda${errorNombre ? ' reservas-campo-ayuda--error' : ''}`}
            aria-live="polite"
          >
            {errorNombre}
          </span>
        </div>

        <div className="reservas-campo reservas-campo--separado">
          <label className="reservas-campo-etiqueta" htmlFor="reservas-telefono">Teléfono</label>
          <div className="reservas-campo-control">
            <span className="reservas-campo-icono" aria-hidden="true">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="7" y="2.5" width="10" height="19" rx="2.5" />
                <path d="M11 18.5h2" />
              </svg>
            </span>
            <input
              id="reservas-telefono"
              type="tel"
              inputMode="numeric"
              autoComplete="tel-national"
              className="reservas-input reservas-input--con-icono"
              value={telefono}
              aria-invalid={Boolean(errorTelefono)}
              aria-describedby="reservas-ayuda-telefono"
              onChange={(evento) => onCambiar({ telefono: evento.target.value.replace(/\D/g, '').slice(0, 11) })}
              onBlur={() => setTocado((t) => ({ ...t, telefono: true }))}
            />
          </div>
          <span
            id="reservas-ayuda-telefono"
            className={`reservas-campo-ayuda${errorTelefono ? ' reservas-campo-ayuda--error' : ''}`}
            aria-live="polite"
          >
            {errorTelefono || AYUDA_TELEFONO_FORMATO}
          </span>
        </div>

        <div className="reservas-aviso reservas-aviso--info" role="note">
          <span className="reservas-aviso-icono" aria-hidden="true">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="4" y="11" width="16" height="9" rx="2" />
              <path d="M8 11V8a4 4 0 0 1 8 0v3" />
            </svg>
          </span>
          <span><strong>Tu teléfono es tu llave.</strong> Lo usamos solo para identificar tu reserva y no lo mostramos en ningún enlace. Puedes tener hasta 2 reservas vigentes con el mismo número.</span>
        </div>

        {errorEnvio?.tipo === '409-limite' && (
          <Aviso tipo="error">
            {errorEnvio.mensaje} <Link to="/mi-reserva">Ir a Mi reserva</Link>
          </Aviso>
        )}
        {errorEnvio && errorEnvio.tipo !== '409-limite' && (
          <Aviso
            tipo={errorEnvio.tipo === '429' ? 'advertencia' : 'error'}
            rol={errorEnvio.tipo === '429' ? 'alert' : undefined}
          >
            {errorEnvio.mensaje}
          </Aviso>
        )}

        <div className="reservas-acciones-paso">
          <button type="button" className="reservas-boton reservas-boton--secundario" onClick={onAtras} disabled={enviandoAhora}>
            <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M19 12H5M11 6l-6 6 6 6" />
            </svg>
            Atrás
          </button>
          <button type="submit" className="reservas-boton reservas-boton--primario" disabled={enviandoAhora || bloqueado429}>
            {enviandoAhora ? 'Confirmando…' : (
              <>
                Confirmar reserva
                <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M5 12.5l4.5 4.5L19 7.5" />
                </svg>
              </>
            )}
          </button>
        </div>
      </form>
    </section>
  );
}
