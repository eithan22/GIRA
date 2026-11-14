import { useEffect, useState } from 'react';
import Aviso from '../../components/Aviso.jsx';
import { formatearFechaCorta, formatearHora } from '../../reservasFormato.js';
import { useMesasLibres } from '../hooks/useMesasLibres.js';
import { useParametrosOperativos } from '../hooks/useParametrosOperativos.js';
import {
  armarModificacion,
  esFechaIso,
  fechaEnRango,
  hayCambios,
  mesasSeleccionables,
  rangoFechasReserva,
  resumenCambios,
  sumarDias,
  validarPersonas,
} from '../reservasPersonalFormato.js';
import {
  ayudaNuevoHorario,
  CUERPO_CONFIRMAR_MODIFICAR,
  ETIQUETA_MESA,
  mensajeFechaFueraDeRango,
  OPCION_MESA_AUTOMATICA,
  TEXTO_ACTUALIZAR_MESAS,
  TEXTO_CARGANDO_MESAS,
  TEXTO_CONFIRMAR_MODIFICAR,
  TEXTO_MESAS_NO_CONFIRMADAS,
  TEXTO_MESA_ACTUAL,
  TEXTO_MODIFICANDO,
  TEXTO_REVISAR_CAMBIOS,
  TEXTO_SIN_CAMBIOS,
  TEXTO_VOLVER,
  TEXTO_VOLVER_A_EDITAR,
  TITULO_CONFIRMAR_MODIFICAR,
  textoMesasLibres,
  textoSinMesasLibres,
} from '../reservasPersonalTextos.js';
import ResumenCambios from './ResumenCambios.jsx';

function textoFranja(franja) {
  return `${franja.nombre} · ${formatearHora(franja.horaInicio)}–${formatearHora(franja.horaFin)}`;
}

/**
 * Modificar una reserva Confirmada: fecha, franja, personas y mesa. Las mesas salen de
 * GET /api/reservas/ocupacion de la fecha y franja elegidas (libres con capacidad suficiente más la
 * mesa actual); «Automática» envía mesaId vacío y el servidor conserva la actual si sirve. Antes de
 * enviar se muestra el antes/después y se pide confirmación explícita. Lo escrito vive aquí, así que
 * un error o un refresco de la lista no lo pierden. `onEnviar` devuelve true si el servidor aceptó.
 */
export default function FormularioModificar({
  detalle, franjas, franjasFallaron, hoy, enviando, onEnviar, onVolver, onBorrador,
}) {
  const franjasActivas = franjas.filter((franja) => franja.activa);
  const [fecha, setFecha] = useState(detalle.fecha);
  const [franjaId, setFranjaId] = useState(franjasActivas.some((franja) => franja.id === detalle.franjaId) ? detalle.franjaId : '');
  const [personas, setPersonas] = useState(String(detalle.personas));
  const [mesaId, setMesaId] = useState('');
  const [fase, setFase] = useState('editar'); // 'editar' | 'confirmar'

  const mesas = useMesasLibres({ fecha, franjaId });
  const { antelacionMaximaDias } = useParametrosOperativos();
  const { min, max } = rangoFechasReserva(hoy, antelacionMaximaDias);

  const validacionPersonas = validarPersonas(personas);
  // Si la fecha no cambió, el servidor no la revalida; solo se exige rango cuando el usuario la mueve.
  const fechaValida = esFechaIso(fecha) && (fecha === detalle.fecha || fechaEnRango(fecha, hoy, antelacionMaximaDias));
  const franjaValida = franjaId !== '';

  const opciones = mesas.mapa && validacionPersonas.valido
    ? mesasSeleccionables(mesas.mapa, validacionPersonas.valor, detalle.id)
    : [];
  const mesaEfectiva = opciones.some((opcion) => opcion.mesaId === mesaId) ? mesaId : '';
  const mesaElegida = opciones.find((opcion) => opcion.mesaId === mesaEfectiva);
  const sinMesas = mesas.estado === 'listo' && validacionPersonas.valido && opciones.length === 0;

  const franjaElegida = franjasActivas.find((franja) => franja.id === franjaId);
  const filas = resumenCambios(
    {
      fecha: detalle.fecha,
      fechaTexto: formatearFechaCorta(detalle.fecha),
      franjaId: detalle.franjaId,
      franjaTexto: `${detalle.franjaNombre} · ${formatearHora(detalle.franjaHoraInicio)}–${formatearHora(detalle.franjaHoraFin)}`,
      personas: detalle.personas,
      mesaId: detalle.mesaId,
      mesaTexto: `Mesa ${detalle.mesaNumero} · ${detalle.zonaNombre}`,
    },
    {
      fecha,
      fechaTexto: esFechaIso(fecha) ? formatearFechaCorta(fecha) : '—',
      franjaId,
      franjaTexto: franjaElegida ? textoFranja(franjaElegida) : '—',
      personas: validacionPersonas.valido ? validacionPersonas.valor : personas,
      mesaId: mesaEfectiva,
      mesaTexto: mesaElegida ? `Mesa ${mesaElegida.numero} · ${mesaElegida.zonaNombre}` : OPCION_MESA_AUTOMATICA,
    },
  );
  const hay = hayCambios(filas);
  const puedeRevisar = hay && fechaValida && franjaValida && validacionPersonas.valido && !sinMesas && !enviando;

  // El panel pregunta antes de cerrarse si hay cambios sin guardar.
  useEffect(() => {
    onBorrador(hay);
    return () => onBorrador(false);
  }, [hay, onBorrador]);

  async function confirmar() {
    const aceptada = await onEnviar(armarModificacion({
      fecha, franjaId, personas: validacionPersonas.valor, mesaId: mesaEfectiva,
    }));
    if (!aceptada) setFase('editar');
  }

  if (fase === 'confirmar') {
    return (
      <section className="reservas-personal-confirmacion" aria-labelledby="modificar-confirmar-titulo">
        <h3 id="modificar-confirmar-titulo" className="reservas-personal-confirmacion-titulo">{TITULO_CONFIRMAR_MODIFICAR}</h3>
        <p className="reservas-personal-confirmacion-texto">{CUERPO_CONFIRMAR_MODIFICAR}</p>
        <ResumenCambios filas={filas} />
        <div className="reservas-personal-confirmacion-botones">
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
            onClick={confirmar}
            disabled={enviando}
          >{enviando ? TEXTO_MODIFICANDO : TEXTO_CONFIRMAR_MODIFICAR}</button>
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={() => setFase('editar')}
            disabled={enviando}
          >{TEXTO_VOLVER_A_EDITAR}</button>
        </div>
      </section>
    );
  }

  return (
    <form
      className="reservas-personal-formulario"
      onSubmit={(evento) => { evento.preventDefault(); if (puedeRevisar) setFase('confirmar'); }}
      noValidate
    >
      <div className="reservas-personal-fila-campos">
        <div className="reservas-campo">
          <label className="reservas-campo-etiqueta" htmlFor="mod-fecha">Fecha</label>
          <div className="reservas-personal-fecha">
            <button
              type="button"
              className="reservas-personal-icono-boton"
              aria-label="Día anterior"
              onClick={() => setFecha(sumarDias(fecha, -1))}
              disabled={!esFechaIso(fecha) || fecha <= min}
            >‹</button>
            <input
              id="mod-fecha"
              type="date"
              className="reservas-input"
              min={min}
              max={max}
              value={fecha}
              onChange={(evento) => setFecha(evento.target.value)}
              aria-invalid={fechaValida ? undefined : 'true'}
              aria-describedby="mod-fecha-ayuda"
            />
            <button
              type="button"
              className="reservas-personal-icono-boton"
              aria-label="Día siguiente"
              onClick={() => setFecha(sumarDias(fecha, 1))}
              disabled={!esFechaIso(fecha) || fecha >= max}
            >›</button>
          </div>
          <span id="mod-fecha-ayuda" className={`reservas-campo-ayuda${fechaValida ? '' : ' reservas-campo-ayuda--error'}`}>
            {fechaValida ? ayudaNuevoHorario(antelacionMaximaDias) : mensajeFechaFueraDeRango(antelacionMaximaDias)}
          </span>
        </div>

        <div className="reservas-campo">
          <label className="reservas-campo-etiqueta" htmlFor="mod-personas">Personas</label>
          <input
            id="mod-personas"
            type="number"
            inputMode="numeric"
            min={1}
            step={1}
            autoComplete="off"
            className="reservas-input"
            value={personas}
            onChange={(evento) => setPersonas(evento.target.value)}
            aria-invalid={validacionPersonas.valido ? undefined : 'true'}
            aria-describedby={validacionPersonas.valido ? undefined : 'mod-personas-error'}
          />
          {!validacionPersonas.valido && (
            <span id="mod-personas-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">
              {validacionPersonas.mensaje}
            </span>
          )}
        </div>
      </div>

      <fieldset className="reservas-personal-grupo">
        <legend className="reservas-campo-etiqueta">Franja</legend>
        {franjasFallaron && <Aviso tipo="error">No pudimos cargar las franjas.</Aviso>}
        <div className="reservas-personal-opciones">
          {franjasActivas.map((franja) => (
            <label key={franja.id} className="reservas-personal-opcion">
              <input
                type="radio"
                name="franja-modificar"
                value={franja.id}
                checked={franjaId === franja.id}
                onChange={() => setFranjaId(franja.id)}
              />
              <span><strong>{textoFranja(franja)}</strong></span>
            </label>
          ))}
        </div>
      </fieldset>

      <div className="reservas-campo">
        <label className="reservas-campo-etiqueta" htmlFor="mod-mesa">{ETIQUETA_MESA}</label>
        <select
          id="mod-mesa"
          className="reservas-input"
          value={mesaEfectiva}
          onChange={(evento) => setMesaId(evento.target.value)}
          disabled={mesas.estado === 'cargando'}
          aria-describedby="mod-mesa-estado"
        >
          <option value="">{OPCION_MESA_AUTOMATICA}</option>
          {opciones.map((opcion) => (
            <option key={opcion.mesaId} value={opcion.mesaId}>
              {`Mesa ${opcion.numero} · ${opcion.zonaNombre} · capacidad ${opcion.capacidad}${opcion.actual ? ` (${TEXTO_MESA_ACTUAL})` : ''}`}
            </option>
          ))}
        </select>
        <p id="mod-mesa-estado" className="reservas-campo-ayuda" role="status" aria-live="polite">
          {mesas.estado === 'cargando' && TEXTO_CARGANDO_MESAS}
          {mesas.estado === 'listo' && !sinMesas && textoMesasLibres(opciones.length)}
        </p>
        {sinMesas && <Aviso tipo="neutro">{textoSinMesasLibres(validacionPersonas.valor)}</Aviso>}
        {mesas.estado === 'error' && (
          <div className="reservas-personal-bloque-error">
            <Aviso tipo="error">{mesas.error}</Aviso>
            <Aviso tipo="info">{TEXTO_MESAS_NO_CONFIRMADAS}</Aviso>
          </div>
        )}
        {(mesas.estado === 'listo' || mesas.estado === 'error') && (
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={mesas.reintentar}
          >{TEXTO_ACTUALIZAR_MESAS}</button>
        )}
      </div>

      <ResumenCambios filas={filas} />
      {!hay && <p className="reservas-campo-ayuda">{TEXTO_SIN_CAMBIOS}</p>}

      <div className="reservas-personal-confirmacion-botones">
        <button
          type="submit"
          className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
          disabled={!puedeRevisar}
        >{TEXTO_REVISAR_CAMBIOS}</button>
        <button
          type="button"
          className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
          onClick={onVolver}
          disabled={enviando}
        >{TEXTO_VOLVER}</button>
      </div>
    </form>
  );
}
