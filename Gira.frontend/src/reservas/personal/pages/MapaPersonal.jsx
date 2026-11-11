import { useCallback, useEffect, useRef, useState } from 'react';
import { formatearHora } from '../../reservasFormato.js';
import EstadoPagina from '../components/EstadoPagina.jsx';
import PaginaPersonal from '../components/PaginaPersonal.jsx';
import PanelDetalleReserva from '../components/PanelDetalleReserva.jsx';
import PanelVisita from '../components/PanelVisita.jsx';
import ResumenMapa from '../components/ResumenMapa.jsx';
import ZonaMapa from '../components/ZonaMapa.jsx';
import { useAhora } from '../hooks/useAhora.js';
import { useFranjas } from '../hooks/useFranjas.js';
import { useMesasLibres } from '../hooks/useMesasLibres.js';
import {
  agruparMesasPorZona,
  esFechaIso,
  filaDesdeMesa,
  franjaPorDefecto,
  puedeRegistrarVisita,
  sumarDias,
} from '../reservasPersonalFormato.js';
import {
  AYUDA_ACTUALIZAR_CADA_60,
  ETIQUETA_ACTUALIZADO,
  TEXTO_ACTUALIZACION_DETENIDA,
  TEXTO_ACTUALIZAR_CADA_60,
  TEXTO_CARGANDO_MAPA,
  TEXTO_ELIGE_FRANJA_MAPA,
  TEXTO_FRANJA_INACTIVA,
  TEXTO_SIN_FRANJAS_MAPA,
  TEXTO_SIN_ZONAS,
  TEXTO_VISITA_NO_DISPONIBLE,
} from '../reservasPersonalTextos.js';

function etiquetaFranja(franja) {
  const base = `${franja.nombre} · ${formatearHora(franja.horaInicio)}–${formatearHora(franja.horaFin)}`;
  return franja.activa ? base : `${base} (${TEXTO_FRANJA_INACTIVA})`;
}

/**
 * Ruta /reservas/mapa (Lote 4, GI-396): mapa de mesas por fecha y franja. Las mesas con reserva
 * abren su reserva en PanelDetalleReserva (con datos parciales que se completan al cargar); las
 * libres abren «Registrar visita» solo si es hoy y la franja activa ya empezó (hora de Santo
 * Domingo; el servidor decide). Actualización manual y, si se activa, cada 60 s.
 */
export default function MapaPersonal() {
  const { hoy, hora } = useAhora();
  const franjasApi = useFranjas();
  const { franjas } = franjasApi;

  const [fecha, setFecha] = useState(hoy);
  const [franjaId, setFranjaId] = useState('');
  const [auto, setAuto] = useState(false);
  const [panel, setPanel] = useState(null); // { tipo: 'reserva', fila } | { tipo: 'visita', mesaId }
  const origenRef = useRef(null);
  const tituloRef = useRef(null);
  const yaEligioDefectoRef = useRef(false);

  // Franja por defecto: una sola vez, cuando llegan las franjas (después manda lo que elija el usuario).
  useEffect(() => {
    if (yaEligioDefectoRef.current || franjasApi.estado !== 'listo') return;
    yaEligioDefectoRef.current = true;
    const porDefecto = franjaPorDefecto(franjas, { fecha, hoy, hora });
    if (porDefecto) setFranjaId(porDefecto.id);
  }, [franjasApi.estado, franjas, fecha, hoy, hora]);

  const mesas = useMesasLibres({ fecha, franjaId, autoRefresco: auto, pausado: Boolean(panel) });
  const franjaSeleccionada = franjas.find((franja) => franja.id === franjaId) ?? null;

  // Un mapa de otra fecha o franja (mientras llega el nuevo) no se ofrece como actual.
  const mapa = mesas.mapa && mesas.mapa.franjaId === franjaId && mesas.mapa.fecha === fecha ? mesas.mapa : null;
  const zonas = agruparMesasPorZona(mapa);
  const puedeVisita = puedeRegistrarVisita({ fecha, hoy, franja: franjaSeleccionada, hora });

  const alActualizarReserva = useCallback(() => mesas.reintentar(), [mesas]);

  function abrirReserva(zona, mesa, boton) {
    origenRef.current = boton;
    setPanel({ tipo: 'reserva', fila: filaDesdeMesa(mapa, zona, mesa) });
  }

  function abrirVisita(zona, mesa, boton) {
    origenRef.current = boton;
    setPanel({ tipo: 'visita', mesaId: mesa.mesaId });
  }

  const cargandoMapa = Boolean(franjaId) && !mapa && (mesas.estado === 'cargando' || mesas.estado === 'idle');
  const hayFranjas = franjas.length > 0;

  return (
    <PaginaPersonal clave="mapa" tituloRef={tituloRef}>
      <div className="reservas-personal-tarjeta reservas-personal-filtros">
        <div className="reservas-personal-filtros-grid">
          <div className="reservas-campo">
            <label className="reservas-campo-etiqueta" htmlFor="mapa-fecha">Fecha</label>
            <div className="reservas-personal-fecha">
              <button
                type="button"
                className="reservas-personal-icono-boton"
                aria-label="Día anterior"
                onClick={() => setFecha(sumarDias(fecha, -1))}
                disabled={!esFechaIso(fecha)}
              >‹</button>
              <input
                id="mapa-fecha"
                type="date"
                className="reservas-input"
                value={fecha}
                onChange={(evento) => { if (esFechaIso(evento.target.value)) setFecha(evento.target.value); }}
              />
              <button
                type="button"
                className="reservas-personal-icono-boton"
                aria-label="Día siguiente"
                onClick={() => setFecha(sumarDias(fecha, 1))}
                disabled={!esFechaIso(fecha)}
              >›</button>
              <button
                type="button"
                className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
                onClick={() => setFecha(hoy)}
              >Hoy</button>
            </div>
          </div>

          <div className="reservas-campo">
            <label className="reservas-campo-etiqueta" htmlFor="mapa-franja">Franja</label>
            <select
              id="mapa-franja"
              className="reservas-input"
              value={franjaId}
              onChange={(evento) => setFranjaId(evento.target.value)}
              disabled={!hayFranjas}
            >
              <option value="">Elige una franja</option>
              {franjas.map((franja) => (
                <option key={franja.id} value={franja.id}>{etiquetaFranja(franja)}</option>
              ))}
            </select>
          </div>
        </div>

        <div className="reservas-personal-resumen">
          <label className="reservas-personal-auto">
            <input type="checkbox" checked={auto} onChange={(evento) => setAuto(evento.target.checked)} />
            <span>
              {TEXTO_ACTUALIZAR_CADA_60}
              <small className="reservas-personal-secundario">{AYUDA_ACTUALIZAR_CADA_60}</small>
            </span>
          </label>
          <div className="reservas-personal-resumen-derecha">
            {mesas.actualizadoA && (
              <span className="reservas-personal-actualizado">{ETIQUETA_ACTUALIZADO} {mesas.actualizadoA}</span>
            )}
            <button
              type="button"
              className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
              onClick={mesas.reintentar}
              disabled={!franjaId || mesas.estado === 'cargando'}
            >{mesas.estado === 'cargando' ? 'Actualizando…' : 'Actualizar'}</button>
          </div>
        </div>
        {mesas.detenido && auto && <p className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{TEXTO_ACTUALIZACION_DETENIDA}</p>}
      </div>

      {franjasApi.estado === 'cargando' && <EstadoPagina estado="cargando" />}
      {franjasApi.estado === 'error' && (
        <EstadoPagina estado="error" mensaje={franjasApi.error} onReintentar={franjasApi.reintentar} />
      )}
      {franjasApi.estado === 'listo' && !hayFranjas && <EstadoPagina estado="vacio" mensaje={TEXTO_SIN_FRANJAS_MAPA} />}
      {franjasApi.estado === 'listo' && hayFranjas && !franjaId && (
        <EstadoPagina estado="vacio" mensaje={TEXTO_ELIGE_FRANJA_MAPA} />
      )}

      {cargandoMapa && <EstadoPagina estado="cargando" mensaje={TEXTO_CARGANDO_MAPA} />}
      {mesas.estado === 'error' && !mapa && (
        <EstadoPagina estado="error" mensaje={mesas.error} onReintentar={mesas.reintentar} />
      )}

      {mapa && (
        <div className="reservas-personal-mapa" aria-busy={mesas.estado === 'cargando' ? 'true' : 'false'}>
          <ResumenMapa mapa={mapa} />
          {!puedeVisita && <p className="reservas-campo-ayuda">{TEXTO_VISITA_NO_DISPONIBLE}</p>}
          {zonas.length === 0 && <EstadoPagina estado="vacio" mensaje={TEXTO_SIN_ZONAS} />}
          {zonas.map((zona) => (
            <ZonaMapa
              key={zona.zonaId}
              zona={zona}
              puedeVisita={puedeVisita}
              onAbrirReserva={abrirReserva}
              onRegistrarVisita={abrirVisita}
            />
          ))}
        </div>
      )}

      {panel?.tipo === 'reserva' && (
        <PanelDetalleReserva
          key={panel.fila.id}
          filaInicial={panel.fila}
          franjas={franjas}
          franjasFallaron={franjasApi.estado === 'error'}
          origenRef={origenRef}
          fallbackFocoRef={tituloRef}
          onCerrar={() => setPanel(null)}
          onActualizada={alActualizarReserva}
          onNoExiste={alActualizarReserva}
        />
      )}

      {panel?.tipo === 'visita' && (
        <PanelVisita
          franjas={franjas}
          mesaInicialId={panel.mesaId}
          origenRef={origenRef}
          fallbackFocoRef={tituloRef}
          onCerrar={() => setPanel(null)}
        />
      )}
    </PaginaPersonal>
  );
}
