import { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { formatearFechaLarga } from '../../reservasFormato.js';
import EstadoPagina from '../components/EstadoPagina.jsx';
import FiltrosAgenda from '../components/FiltrosAgenda.jsx';
import ListaReservas from '../components/ListaReservas.jsx';
import PaginaPersonal from '../components/PaginaPersonal.jsx';
import PaginacionPersonal from '../components/PaginacionPersonal.jsx';
import PanelDetalleReserva from '../components/PanelDetalleReserva.jsx';
import PanelNuevaReserva from '../components/PanelNuevaReserva.jsx';
import { useAgendaReservas } from '../hooks/useAgendaReservas.js';
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
 * Ruta /reservas/agenda: agenda del día con filtros, búsqueda por teléfono y acciones en un panel
 * lateral (Lote 2, GI-394), más «Nueva reserva» y «Modificar» (Lote 3, GI-395).
 */
export default function AgendaPersonal() {
  const agenda = useAgendaReservas();
  const { aplicados, items, total, fase, recargando, errorLista, actualizadoA, resaltado, cargas } = agenda;

  // La reserva abierta en el panel es una copia propia: el panel no depende de que la fila siga en la lista.
  const [seleccion, setSeleccion] = useState(null);
  const [creando, setCreando] = useState(false);
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
    setSeleccion(fila);
  }

  function abrirNueva(evento) {
    origenRef.current = evento.currentTarget;
    setSeleccion(null);
    setCreando(true);
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
            onClick={agenda.actualizar}
            disabled={recargando}
          >{recargando ? 'Actualizando…' : 'Actualizar'}</button>
          <Link
            to="/reservas/visitas"
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
          >{TEXTO_BOTON_VISITA_SIN_RESERVA}</Link>
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
            onClick={abrirNueva}
          >{TEXTO_NUEVA_RESERVA}</button>
        </div>
      </div>

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
    </PaginaPersonal>
  );
}
