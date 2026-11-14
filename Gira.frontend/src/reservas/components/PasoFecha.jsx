import { useEffect, useRef, useState } from 'react';
import { reservasService, esPeticionCancelada, extraerMensajeError } from '../reservasService.js';
import {
  cantidadChipsPersonas,
  diaCerradoEnFranjas,
  estadoFranjaCliente,
  franjaSigueDisponible,
  generarDiasChip,
  obtenerLimiteFechas,
  usaCampoNumericoPersonas,
  validarPersonasCliente,
} from '../reservasFormato.js';
import { usePoliticasReserva } from '../hooks/usePoliticasReserva.js';
import {
  AVISO_DIA_CERRADO,
  BADGE_FRANJA_DISPONIBLE,
  BADGE_FRANJA_NO_DISPONIBLE,
  ERROR_PERSONAS_FORMATO,
  ERROR_PERSONAS_MINIMO,
  ETIQUETA_PERSONAS_NUMERO,
  TELEFONO_RESTAURANTE,
  TEXTO_CARGANDO_DISPONIBILIDAD,
  TEXTO_SIN_DISPONIBILIDAD,
  MENSAJE_ERROR_GENERICO,
  textoAyudaPersonas,
  textoGrupoGrande,
  textoMasDePersonas,
  textoVentanaReserva,
} from '../reservasTextos.js';
import Aviso from './Aviso.jsx';

// El tamaño máximo de grupo, la ventana de fechas y los textos salen de las políticas del servidor
// (usePoliticasReserva); el servidor sigue decidiendo cada reserva.
const DEMORA_DEBOUNCE_MS = 350;
const VIGENCIA_CACHE_MS = 30_000;
const DURACION_BLOQUEO_429_MS = 10_000;
// "Noche" por horario, no por nombre: una franja que arranca a las 17:00 o
// después usa el icono de luna, sin depender de que se llame "Cena".
const HORA_INICIO_NOCHE = '17:00';

function IconoFranja({ nocturna }) {
  if (nocturna) {
    return (
      <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#e6e4fa" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z" />
      </svg>
    );
  }

  return (
    <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="#6b4300" strokeWidth="2" strokeLinecap="round">
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
    </svg>
  );
}

/** Paso 1 del wizard de /reservar: fecha, personas y franja horaria. */
export default function PasoFecha({ valor, onCambiar, onContinuar }) {
  const { fecha, personas, franja } = valor;
  const { politicas } = usePoliticasReserva();
  const dias = generarDiasChip(5, politicas);
  const limites = obtenerLimiteFechas(politicas);
  const campoNumerico = usaCampoNumericoPersonas(politicas);
  const [personasTexto, setPersonasTexto] = useState(String(personas));
  const validacionPersonas = validarPersonasCliente(personasTexto, politicas.maxPersonas);

  const [franjas, setFranjas] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState(null);
  const [bloqueadoHasta, setBloqueadoHasta] = useState(0);

  const cacheRef = useRef(new Map());
  const abortRef = useRef(null);
  const debounceRef = useRef(null);
  const bloqueoTimerRef = useRef(null);
  const peticionIdRef = useRef(0);

  useEffect(() => {
    // Se marca "cargando" de inmediato, antes de que corra el debounce: así
    // Continuar queda bloqueado desde el instante en que cambia fecha/personas,
    // no solo cuando arranca la petición 350ms después. Si ya está bloqueado
    // por un 429, no hay nada que cargar: se apaga el spinner.
    if (Date.now() < bloqueadoHasta) {
      setCargando(false);
    } else {
      setCargando(true);
    }

    if (debounceRef.current) clearTimeout(debounceRef.current);

    debounceRef.current = setTimeout(() => {
      if (Date.now() < bloqueadoHasta) return;
      consultar();
    }, DEMORA_DEBOUNCE_MS);

    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
      if (abortRef.current) abortRef.current.abort();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fecha, personas, bloqueadoHasta]);

  useEffect(() => () => {
    if (bloqueoTimerRef.current) clearTimeout(bloqueoTimerRef.current);
  }, []);

  // Si las políticas llegan con un grupo máximo menor que las personas elegidas, se baja al máximo.
  useEffect(() => {
    if (personas > politicas.maxPersonas) onCambiar({ personas: politicas.maxPersonas });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [personas, politicas.maxPersonas]);

  // El campo numérico sigue a las personas cuando cambian por otro lado (por ejemplo, el ajuste de arriba).
  useEffect(() => {
    setPersonasTexto(String(personas));
  }, [personas]);

  function cambiarPersonasTexto(texto) {
    setPersonasTexto(texto);
    const validacion = validarPersonasCliente(texto, politicas.maxPersonas);
    if (validacion.valido) onCambiar({ personas: validacion.valor });
  }

  async function consultar() {
    const clave = `${fecha}|${personas}`;
    const cacheada = cacheRef.current.get(clave);
    if (cacheada && Date.now() - cacheada.momento < VIGENCIA_CACHE_MS) {
      aplicarFranjas(cacheada.franjas);
      return;
    }

    if (abortRef.current) abortRef.current.abort();
    const controlador = new AbortController();
    abortRef.current = controlador;

    // El abort() de arriba normalmente evita que una petición vieja resuelva
    // con éxito, pero si su respuesta ya había llegado en el mismo instante,
    // abortar no la revierte. El id de petición cubre esa ventana: si para
    // cuando la promesa resuelve ya hay una petición más nueva en curso, el
    // resultado se descarta aunque no haya sido técnicamente "cancelado".
    const idPeticion = ++peticionIdRef.current;

    setCargando(true);
    setError(null);

    try {
      const resultado = await reservasService.consultarDisponibilidad(
        { fecha, personas },
        { signal: controlador.signal },
      );
      if (idPeticion !== peticionIdRef.current) return;
      cacheRef.current.set(clave, { franjas: resultado, momento: Date.now() });
      aplicarFranjas(resultado);
    } catch (err) {
      if (esPeticionCancelada(err) || idPeticion !== peticionIdRef.current) return;
      setCargando(false);
      setFranjas([]);

      if (err.response?.status === 429) {
        setError({ tipo: '429', mensaje: extraerMensajeError(err, MENSAJE_ERROR_GENERICO) });
        setBloqueadoHasta(Date.now() + DURACION_BLOQUEO_429_MS);
        if (bloqueoTimerRef.current) clearTimeout(bloqueoTimerRef.current);
        bloqueoTimerRef.current = setTimeout(() => setBloqueadoHasta(0), DURACION_BLOQUEO_429_MS);
        return;
      }

      let mensaje = extraerMensajeError(err, MENSAJE_ERROR_GENERICO);
      if (mensaje.toLowerCase().includes('llama al restaurante')) {
        mensaje = `${mensaje} ${TELEFONO_RESTAURANTE}`;
      }
      setError({ tipo: 'otro', mensaje });
    }
  }

  function aplicarFranjas(lista) {
    setFranjas(lista);
    setCargando(false);
    // La franja elegida debe seguir existiendo y poder elegirse en la lista nueva.
    if (franja && !franjaSigueDisponible(franja, lista)) {
      onCambiar({ franja: null });
    }
  }

  const sinResultados = !cargando && !error && franjas.length === 0;
  const diaCerrado = diaCerradoEnFranjas(franjas);
  // Con el campo numérico, un valor inválido no cambia `personas`: se bloquea Continuar para no avanzar con el anterior.
  const personasValidas = !campoNumerico || validacionPersonas.valido;

  return (
    <section>
      <ol className="reservas-pasos">
        <li className="reservas-paso reservas-paso--activo" aria-current="step">
          <span className="reservas-paso-numero reservas-paso-numero--activo">1</span>
          <strong>Fecha y franja</strong>
        </li>
        <li className="reservas-paso-separador" aria-hidden="true" />
        <li className="reservas-paso">
          <span className="reservas-paso-numero">2</span>
          <span>Tus datos</span>
        </li>
      </ol>

      <div className="reservas-seccion-cabecera">
        <h2 id="reservas-h-dia" className="reservas-seccion-titulo">¿Qué día vienes?</h2>
        <label className="reservas-otra-fecha" htmlFor="reservas-otra-fecha">
          <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <rect x="3" y="5" width="18" height="16" rx="2" />
            <path d="M3 10h18M8 3v4M16 3v4" />
          </svg>
          Otra fecha
          <input
            id="reservas-otra-fecha"
            type="date"
            className="reservas-otra-fecha-input"
            value={fecha}
            min={limites.min}
            max={limites.max}
            onChange={(evento) => {
              // El selector nativo dispara onChange con "" al borrar la fecha
              // (por ejemplo, con la tecla Supr sobre el input): se ignora en
              // vez de dejar fecha="" en el estado, que rompería generarDiasChip
              // y el min/max del propio input.
              if (!evento.target.value) return;
              onCambiar({ fecha: evento.target.value });
            }}
          />
        </label>
      </div>

      <div className="reservas-chips-dia" role="group" aria-labelledby="reservas-h-dia">
        {dias.map((d) => (
          <button
            key={d.iso}
            type="button"
            aria-pressed={d.iso === fecha}
            className={`reservas-chip-dia${d.iso === fecha ? ' reservas-chip-dia--activo' : ''}`}
            onClick={() => onCambiar({ fecha: d.iso })}
          >
            <span className="reservas-chip-dia-dow">{d.dow}</span>
            <span className="reservas-chip-dia-num">{d.num}</span>
            <span className="reservas-chip-dia-mes">{d.mes}</span>
          </button>
        ))}
      </div>

      <h2 id="reservas-h-pers" className="reservas-seccion-titulo">¿Cuántos son?</h2>
      {campoNumerico ? (
        <div className="reservas-campo reservas-campo--personas">
          <label className="reservas-campo-etiqueta" htmlFor="reservas-personas-numero">{ETIQUETA_PERSONAS_NUMERO}</label>
          <input
            id="reservas-personas-numero"
            type="number"
            inputMode="numeric"
            min={1}
            max={politicas.maxPersonas}
            step={1}
            autoComplete="off"
            className="reservas-input reservas-input--personas"
            value={personasTexto}
            onChange={(evento) => cambiarPersonasTexto(evento.target.value)}
            aria-invalid={validacionPersonas.valido ? undefined : 'true'}
            aria-describedby="reservas-personas-ayuda"
          />
          <span
            id="reservas-personas-ayuda"
            role={validacionPersonas.valido ? undefined : 'alert'}
            className={`reservas-campo-ayuda${validacionPersonas.valido ? '' : ' reservas-campo-ayuda--error'}`}
          >
            {validacionPersonas.error === 'maximo' && textoGrupoGrande(politicas)}
            {validacionPersonas.error === 'minimo' && ERROR_PERSONAS_MINIMO}
            {validacionPersonas.error === 'formato' && ERROR_PERSONAS_FORMATO}
            {validacionPersonas.valido && textoAyudaPersonas(politicas)}
          </span>
        </div>
      ) : (
      <div className="reservas-chips-persona" role="group" aria-labelledby="reservas-h-pers">
        {Array.from({ length: cantidadChipsPersonas(politicas) }, (_valorIgnorado, indice) => indice + 1).map((n) => (
          <button
            key={n}
            type="button"
            aria-pressed={n === personas}
            aria-label={n === 1 ? '1 persona' : `${n} personas`}
            className={`reservas-chip-persona${n === personas ? ' reservas-chip-persona--activo' : ''}`}
            onClick={() => onCambiar({ personas: n })}
          >
            {n}
          </button>
        ))}
      </div>
      )}
      <p className="reservas-nota">
        {textoVentanaReserva(politicas)}
        {!campoNumerico && ` ${textoMasDePersonas(politicas)}`}
      </p>

      <h2 id="reservas-h-franja" className="reservas-seccion-titulo">Elige tu franja</h2>

      {cargando && (
        <div className="reservas-cargando" role="status" aria-live="polite">
          {TEXTO_CARGANDO_DISPONIBILIDAD}
          <span className="reservas-cargando-puntos" aria-hidden="true">
            <span className="reservas-cargando-punto" />
            <span className="reservas-cargando-punto" />
            <span className="reservas-cargando-punto" />
          </span>
        </div>
      )}

      {!cargando && error && (
        <Aviso tipo={error.tipo === '429' ? 'advertencia' : 'error'} rol={error.tipo === '429' ? 'alert' : undefined}>
          {error.mensaje}
        </Aviso>
      )}

      {sinResultados && <Aviso tipo="neutro">{TEXTO_SIN_DISPONIBILIDAD}</Aviso>}

      {!cargando && !error && diaCerrado && <Aviso tipo="neutro">{AVISO_DIA_CERRADO}</Aviso>}

      {!cargando && !error && franjas.length > 0 && (
        <div className="reservas-franjas" role="group" aria-labelledby="reservas-h-franja">
          {franjas.map((f) => {
            const nocturna = f.horaInicio >= HORA_INICIO_NOCHE;
            const { seleccionable, motivo } = estadoFranjaCliente(f);
            const activa = seleccionable && franja?.id === f.id;
            const idMotivo = `reservas-franja-motivo-${f.id}`;
            // No seleccionable: aria-disabled (no disabled) para que siga en el orden de tabulación y el lector
            // pueda leer el motivo; el clic se ignora.
            return (
              <button
                key={f.id}
                type="button"
                aria-pressed={activa}
                aria-disabled={seleccionable ? undefined : 'true'}
                aria-describedby={seleccionable ? undefined : idMotivo}
                className={`reservas-franja${activa ? ' reservas-franja--activa' : ''}${seleccionable ? '' : ' reservas-franja--deshabilitada'}`}
                onClick={() => {
                  if (seleccionable) onCambiar({ franja: f });
                }}
              >
                <span className="reservas-franja-encabezado">
                  <span className={`reservas-franja-icono${nocturna ? ' reservas-franja-icono--noche' : ' reservas-franja-icono--dia'}`}>
                    <IconoFranja nocturna={nocturna} />
                  </span>
                  <span className={`reservas-franja-badge${seleccionable ? '' : ' reservas-franja-badge--no-disponible'}`}>
                    {seleccionable ? BADGE_FRANJA_DISPONIBLE : BADGE_FRANJA_NO_DISPONIBLE}
                  </span>
                </span>
                <span>
                  <strong className="reservas-franja-nombre">{f.nombre}</strong>
                  <span className="reservas-franja-horario">
                    {f.horaInicio.slice(0, 5)} – {f.horaFin.slice(0, 5)}
                  </span>
                </span>
                {!seleccionable && <span id={idMotivo} className="reservas-franja-motivo">{motivo}</span>}
                {activa && (
                  <span className="reservas-franja-check" aria-hidden="true">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3.2" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M5 12.5l4.5 4.5L19 7.5" />
                    </svg>
                  </span>
                )}
              </button>
            );
          })}
        </div>
      )}

      <div className="reservas-acciones-paso">
        <span className="reservas-paso-contador">Paso 1 de 2</span>
        <button
          type="button"
          className="reservas-boton reservas-boton--primario"
          disabled={cargando || !franja || !personasValidas}
          onClick={onContinuar}
        >
          Continuar
          <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M5 12h14M13 6l6 6-6 6" />
          </svg>
        </button>
      </div>
    </section>
  );
}
