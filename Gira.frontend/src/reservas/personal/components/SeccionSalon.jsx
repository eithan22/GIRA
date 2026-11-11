import { useRef, useState } from 'react';
import Aviso from '../../components/Aviso.jsx';
import { useListaSalon } from '../hooks/useListaSalon.js';
import { TAMANO_PAGINA_SALON, activaDeFiltro } from '../reservasPersonalFormato.js';
import {
  AVISO_ZONAS_TRUNCADAS,
  RECURSOS_SALON,
  TEXTO_DESACTIVAR,
  TEXTO_EDITAR,
  TEXTO_FILTRO_ZONA_TODAS,
  TEXTO_REACTIVAR,
  TEXTO_ZONAS_NO_CARGARON,
  textoConteoSalon,
  textoEstadoCambiado,
  textoSinElementos,
} from '../reservasPersonalTextos.js';
import EstadoPagina from './EstadoPagina.jsx';
import FiltroEstadoSalon from './FiltroEstadoSalon.jsx';
import PaginacionPersonal from './PaginacionPersonal.jsx';
import PanelCambioEstado from './PanelCambioEstado.jsx';
import TablaSalon from './TablaSalon.jsx';

/**
 * Sección genérica de Configuración (zonas, mesas o franjas): filtros, lista paginada de 20, botón
 * de crear y los paneles de crear/editar y de cambiar estado. Tras guardar, la fila se actualiza con
 * la respuesta del servidor sin recargar la lista (al crear se recarga, porque la posición cambia) y
 * `onCambio` avisa para refrescar las listas que dependen de este recurso.
 * `Formulario` es el panel de crear/editar del recurso; `zonas` (mesas) alimenta el filtro y el aviso
 * de más de 100 zonas.
 */
export default function SeccionSalon({
  recurso, titulo, columnas, Formulario, nombreDe, zonas, filtroZona = false, onCambio, referenciaTitulo,
}) {
  const textos = RECURSOS_SALON[recurso];
  const [filtroEstado, setFiltroEstado] = useState('todas');
  const [zonaId, setZonaId] = useState('');
  const [page, setPage] = useState(1);
  const [panel, setPanel] = useState(null); // { tipo: 'crear' | 'editar' | 'estado', item? }
  const [mensaje, setMensaje] = useState('');
  const origenRef = useRef(null);
  const tituloRef = useRef(null);

  const lista = useListaSalon({
    recurso, activa: activaDeFiltro(filtroEstado), zonaId: zonaId || undefined, page,
  });

  const conFiltros = filtroEstado !== 'todas' || zonaId !== '';

  function cambiarFiltro(poner) {
    return (valor) => { poner(valor); setPage(1); setMensaje(''); };
  }

  function abrir(nuevo, boton) {
    origenRef.current = boton;
    setMensaje('');
    setPanel(nuevo);
  }

  function alGuardar(dto, esNuevo) {
    setPanel(null);
    if (esNuevo) lista.recargar();
    else lista.reemplazar(dto);
    setMensaje(esNuevo ? textos.creada : textos.actualizada);
    onCambio?.(dto);
  }

  function alCambiarEstado(dto) {
    setPanel(null);
    lista.reemplazar(dto);
    setMensaje(textoEstadoCambiado(textos.singular, dto.activa));
    onCambio?.(dto);
  }

  const acciones = (fila) => (
    <>
      <button
        type="button"
        className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
        onClick={(evento) => abrir({ tipo: 'editar', item: fila }, evento.currentTarget)}
        aria-label={`${TEXTO_EDITAR} ${textos.singular} ${nombreDe(fila)}`}
      >{TEXTO_EDITAR}</button>
      <button
        type="button"
        className={`reservas-boton reservas-boton--chico ${fila.activa ? 'reservas-boton--fantasma-error' : 'reservas-boton--secundario'} reservas-personal-boton-auto`}
        onClick={(evento) => abrir({ tipo: 'estado', item: fila }, evento.currentTarget)}
        aria-label={`${fila.activa ? TEXTO_DESACTIVAR : TEXTO_REACTIVAR} ${textos.singular} ${nombreDe(fila)}`}
      >{fila.activa ? TEXTO_DESACTIVAR : TEXTO_REACTIVAR}</button>
    </>
  );

  return (
    <section className="reservas-personal-seccion" aria-labelledby={`seccion-${recurso}`}>
      <div className="reservas-personal-resumen">
        <h2 id={`seccion-${recurso}`} ref={tituloRef} tabIndex={-1} className="reservas-personal-subtitulo">{titulo}</h2>
        <button
          type="button"
          className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
          onClick={(evento) => abrir({ tipo: 'crear' }, evento.currentTarget)}
        >{textos.nueva}</button>
      </div>

      <div className="reservas-personal-tarjeta reservas-personal-filtros">
        <div className="reservas-personal-filtros-grid">
          {filtroZona && (
            <div className="reservas-campo">
              <label className="reservas-campo-etiqueta" htmlFor="filtro-zona-mesas">Zona</label>
              <select
                id="filtro-zona-mesas"
                className="reservas-input"
                value={zonaId}
                onChange={(evento) => cambiarFiltro(setZonaId)(evento.target.value)}
                disabled={zonas.estado !== 'listo'}
              >
                <option value="">{TEXTO_FILTRO_ZONA_TODAS}</option>
                {zonas.zonas.map((zona) => (
                  <option key={zona.id} value={zona.id}>{zona.activa ? zona.nombre : `${zona.nombre} (inactiva)`}</option>
                ))}
              </select>
            </div>
          )}
          <FiltroEstadoSalon id={`filtro-estado-${recurso}`} valor={filtroEstado} onCambiar={cambiarFiltro(setFiltroEstado)} />
        </div>
        {filtroZona && zonas.estado === 'error' && <Aviso tipo="error">{TEXTO_ZONAS_NO_CARGARON}</Aviso>}
        {filtroZona && zonas.truncado && <Aviso tipo="advertencia">{AVISO_ZONAS_TRUNCADAS}</Aviso>}
      </div>

      <p className="reservas-personal-conteo" role="status" aria-live="polite">
        {lista.estado === 'listo' ? textoConteoSalon(lista.total, textos.singular, textos.plural) : ''}
      </p>
      {mensaje && (
        <div role="status">
          <Aviso tipo="exito">{mensaje}</Aviso>
        </div>
      )}

      {lista.estado === 'cargando' && lista.items.length === 0 && <EstadoPagina estado="cargando" />}
      {lista.estado === 'error' && <EstadoPagina estado="error" mensaje={lista.error} onReintentar={lista.recargar} />}
      {lista.estado === 'listo' && lista.items.length === 0 && (
        <EstadoPagina estado="vacio" mensaje={textoSinElementos(textos.plural, conFiltros)} />
      )}
      {lista.estado !== 'error' && lista.items.length > 0 && (
        <>
          <TablaSalon
            titulo={referenciaTitulo ?? titulo}
            columnas={columnas}
            filas={lista.items}
            acciones={acciones}
            atenuada={(fila) => !fila.activa}
          />
          <PaginacionPersonal
            page={page}
            total={lista.total}
            pageSize={TAMANO_PAGINA_SALON}
            deshabilitada={lista.estado === 'cargando'}
            onIr={(pagina) => { setPage(pagina); setMensaje(''); }}
          />
        </>
      )}

      {(panel?.tipo === 'crear' || panel?.tipo === 'editar') && (
        <Formulario
          key={panel.item?.id ?? 'nuevo'}
          item={panel.item ?? null}
          zonas={zonas}
          origenRef={origenRef}
          fallbackFocoRef={tituloRef}
          onCerrar={() => setPanel(null)}
          onGuardada={alGuardar}
        />
      )}
      {panel?.tipo === 'estado' && (
        <PanelCambioEstado
          key={panel.item.id}
          recurso={recurso}
          item={panel.item}
          activar={!panel.item.activa}
          nombre={nombreDe(panel.item)}
          origenRef={origenRef}
          fallbackFocoRef={tituloRef}
          onCerrar={() => setPanel(null)}
          onCambiada={alCambiarEstado}
        />
      )}
    </section>
  );
}
