import { useCallback, useEffect, useRef, useState } from 'react';
import { formatearFechaLarga } from '../../reservasFormato.js';
import EstadoPagina from '../components/EstadoPagina.jsx';
import FiltrosAgenda from '../components/FiltrosAgenda.jsx';
import ListaReservas from '../components/ListaReservas.jsx';
import PaginaPersonal from '../components/PaginaPersonal.jsx';
import PaginacionPersonal from '../components/PaginacionPersonal.jsx';
import PanelDetalleReserva from '../components/PanelDetalleReserva.jsx';
import PanelNuevaReserva from '../components/PanelNuevaReserva.jsx';
import PanelVisita from '../components/PanelVisita.jsx';
import ResultadoBusquedaCodigo from '../components/ResultadoBusquedaCodigo.jsx';
import SeccionVisitasHoy from '../components/SeccionVisitasHoy.jsx';
import { useAgendaReservas } from '../hooks/useAgendaReservas.js';
import { useVisitasHoy } from '../hooks/useVisitasHoy.js';
import { TAMANO_PAGINA } from '../reservasPersonalFormato.js';
import {
  ETIQUETA_ACTUALIZADO,
  TEXTO_BOTON_VISITA_SIN_RESERVA,
  TEXTO_NUEVA_RESERVA,
  TEXTO_SIN_RESULTADOS_TELEFONO,
  textoConteo,
  textoSinReservas,
} from '../reservasPersonalTextos.js';

/**
 * Ruta /reservas/agenda: agenda del día con filtros, campo único «Código o teléfono» (GI-401) y acciones en un
 * panel lateral (Lote 2, GI-394), más «Nueva reserva» y «Modificar» (Lote 3, GI-395). GI-402: «Visita sin reserva»
 * abre el panel de registro y debajo de la lista va la sección «Visitas sin reserva de hoy» (siempre de HOY, no
 * depende de la fecha ni de los filtros de la agenda); se recarga al montar, tras registrar y con «Actualizar».
 */
export default function AgendaPersonal() {
  const agenda = useAgendaReservas();
  const visitas = useVisitasHoy();
  const { aplicados, items, total, fase, recargando, errorLista, actualizadoA, resaltado, cargas } = agenda;

  // La reserva abierta en el panel es una copia propia: el panel no depende de que la fila siga en la lista.
  const [seleccion, setSeleccion] = useState(null);
  const [creando, setCreando] = useState(false);
  const [registrandoVisita, setRegistrandoVisita] = useState(false);
  const origenRef = useRef(null);
  const tituloRef = useRef(null);
  const resultadosRef = useRef(null);
  const seccionRef = useRef(null);
  const focoPaginaRef = useRef(false);
  const pendienteResaltarRef = useRef(null); // número de cargas al pedir «Ver en la agenda»

  const porTelefono = aplicados.telefono !== '';
  const conFiltros = aplicados.estado !== '' || aplicados.franjaId !== '';
  const fechaLarga = formatearFechaLarga(aplicados.fecha);
  const mostrandoCarga = fase === 'cargando' || (fase === 'error' && recargando);

  // Al cambiar de página el foco pasa al encabezado de resultados cuando llegan.
  useEffect(() => {
    if (!recargando && focoPaginaRef.current) {
      focoPaginaRef.current = false;
      resultadosRef.current?.focus();
    }
  }, [recargando]);

  // Tras «Ver en la agenda»: cuando llega la lista nueva, se enfoca la fila resaltada (si está en esta página).
  useEffect(() => {
    if (pendienteResaltarRef.current === null || cargas <= pendienteResaltarRef.current) return;
    pendienteResaltarRef.current = null;
    if (!resaltado) return;
    const botones = seccionRef.current?.querySelectorAll(`[data-codigo="${resaltado}"] button`) ?? [];
    const visible = [...botones].find((boton) => boton.offsetParent !== null);
    if (visible) {
      visible.focus();
      visible.scrollIntoView({ block: 'center' });
    } else {
      resultadosRef.current?.focus();
    }
  }, [cargas, resaltado]);

  const irAPagina = useCallback((pagina) => {
    focoPaginaRef.current = true;
    agenda.irAPagina(pagina);
  }, [agenda]);

  function abrirDetalle(fila, boton) {
    origenRef.current = boton;
    setCreando(false);
    setRegistrandoVisita(false);
    setSeleccion(fila);
  }

  function abrirNueva(evento) {
    origenRef.current = evento.currentTarget;
    setSeleccion(null);
    setRegistrandoVisita(false);
    setCreando(true);
  }

  // Un solo panel abierto a la vez. «Visita sin reserva» es el mismo PanelVisita que usa el Mapa.
  function abrirVisita(evento) {
    origenRef.current = evento.currentTarget;
    setSeleccion(null);
    setCreando(false);
    setRegistrandoVisita(true);
  }

  // «Actualizar» recarga la lista de reservas, la reserva encontrada por código y las visitas de hoy.
  function actualizarTodo() {
    agenda.actualizar();
    visitas.actualizar();
  }

  function verEnAgenda(fecha, codigo) {
    origenRef.current = null; // el foco irá a la fila resaltada cuando llegue la lista
    pendienteResaltarRef.current = cargas;
    agenda.irAReserva(fecha, codigo);
    setCreando(false);
  }

  // Con la lista vacía solo se anuncia el aviso «No hay reservas…», no también el conteo (sería doble).
  const conteo = fase === 'listo' && items.length > 0 ? textoConteo(total, porTelefono, fechaLarga) : '';

  return (
    <PaginaPersonal clave="agenda" tituloRef={tituloRef}>
      <FiltrosAgenda agenda={agenda} />

      <div className="reservas-personal-resumen">
        <p className="reservas-personal-conteo" role="status" aria-live="polite">{conteo}</p>
        <div className="reservas-personal-resumen-derecha">
          {actualizadoA && (
            <span className="reservas-personal-actualizado">{ETIQUETA_ACTUALIZADO} {actualizadoA}</span>
          )}
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={actualizarTodo}
            disabled={recargando}
          >{recargando ? 'Actualizando…' : 'Actualizar'}</button>
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={abrirVisita}
          >{TEXTO_BOTON_VISITA_SIN_RESERVA}</button>
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
            onClick={abrirNueva}
          >{TEXTO_NUEVA_RESERVA}</button>
        </div>
      </div>

      {/* GI-401: la reserva encontrada por código va encima de la lista y no depende de la fecha ni de los filtros;
          las acciones del panel actualizan esta fila y también la lista del día (agenda.reemplazarFila). */}
      <ResultadoBusquedaCodigo
        resultado={agenda.resultadoCodigo}
        onVer={abrirDetalle}
        onReintentar={agenda.reintentarCodigo}
      />

      <section aria-labelledby="agenda-resultados" ref={seccionRef}>
        <h2 id="agenda-resultados" ref={resultadosRef} tabIndex={-1} className="reservas-personal-subtitulo">Reservas</h2>

        {mostrandoCarga && (items.length === 0 || fase === 'error') && <EstadoPagina estado="cargando" />}
        {fase === 'error' && !recargando && (
          <EstadoPagina estado="error" mensaje={errorLista} onReintentar={agenda.actualizar} />
        )}
        {fase === 'listo' && items.length === 0 && (
          <EstadoPagina
            estado="vacio"
            mensaje={porTelefono ? TEXTO_SIN_RESULTADOS_TELEFONO : textoSinReservas(fechaLarga, conFiltros)}
          />
        )}
        {fase !== 'error' && items.length > 0 && (
          <>
            <ListaReservas
              items={items}
              mostrarFecha={porTelefono}
              ocupada={recargando}
              resaltarCodigo={resaltado}
              onVer={abrirDetalle}
            />
            <PaginacionPersonal
              page={aplicados.page}
              total={total}
              pageSize={TAMANO_PAGINA}
              deshabilitada={recargando}
              onIr={irAPagina}
            />
          </>
        )}
      </section>

      {/* GI-402: visitas sin reserva de HOY, debajo de la agenda para no empujarla hacia abajo. */}
      <SeccionVisitasHoy
        fase={visitas.fase}
        recargando={visitas.recargando}
        datos={visitas.datos}
        error={visitas.error}
        onReintentar={visitas.actualizar}
      />

      {seleccion && (
        <PanelDetalleReserva
          key={seleccion.id}
          filaInicial={seleccion}
          franjas={agenda.franjas}
          franjasFallaron={agenda.franjasFallaron}
          origenRef={origenRef}
          fallbackFocoRef={tituloRef}
          onCerrar={() => setSeleccion(null)}
          onActualizada={agenda.reemplazarFila}
          onNoExiste={agenda.actualizar}
        />
      )}

      {creando && (
        <PanelNuevaReserva
          franjas={agenda.franjas}
          franjasFallaron={agenda.franjasFallaron}
          origenRef={origenRef}
          fallbackFocoRef={tituloRef}
          onCerrar={() => setCreando(false)}
          onCreada={agenda.actualizar}
          onVerEnAgenda={verEnAgenda}
        />
      )}

      {registrandoVisita && (
        <PanelVisita
          franjas={agenda.franjas}
          franjasEstado={agenda.franjasEstado}
          franjasError={agenda.franjasError}
          onReintentarFranjas={agenda.reintentarFranjas}
          origenRef={origenRef}
          fallbackFocoRef={tituloRef}
          onCerrar={() => setRegistrandoVisita(false)}
          onRegistrada={visitas.actualizar}
        />
      )}
    </PaginaPersonal>
  );
}
