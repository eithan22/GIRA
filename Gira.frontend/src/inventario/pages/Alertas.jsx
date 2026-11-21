import { useEffect, useState } from 'react';
import { useAuth } from '../../security/AuthContext.jsx';
import Icon from '../../security/components/Icon.jsx';
import Modal from '../../security/components/Modal.jsx';
import PaginaInventario, { AvisoError, Paginacion } from '../components/PaginaInventario.jsx';
import { ESTADOS_ALERTA, ROLES_GESTIONAR, TIPOS_ALERTA, aNumero, fecha, fechaHora, mensajeError, numero, tieneRol } from '../inventarioFormato.js';
import { inventarioService } from '../inventarioService.js';
import { useCarga } from '../useCarga.js';

const PAGE_SIZE = 20;

/** Ruta /inventario/alertas: panel de alertas (RF-INV-09, RF-INV-10) para atender o descartar. */
export default function Alertas() {
  const { user } = useAuth();
  const puedeConfigurar = tieneRol(user, ROLES_GESTIONAR);
  const [estado, setEstado] = useState('Abierta');
  const [page, setPage] = useState(1);
  const [pagina, setPagina] = useState({ items: [], total: 0 });
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState('');
  const [recarga, setRecarga] = useState(0);
  const [ocupado, setOcupado] = useState(null);
  const { errorCarga, setErrorCarga, reintento, reintentar } = useCarga();
  const [parametros, setParametros] = useState(null);
  const [editandoDias, setEditandoDias] = useState(null); // { proyeccion, vencimiento } como texto
  const [errorModal, setErrorModal] = useState('');

  useEffect(() => {
    let activo = true;
    inventarioService.parametros()
      .then((r) => { if (activo) setParametros(r); })
      .catch((e) => { if (activo) setErrorCarga(mensajeError(e, 'No se pudieron cargar los días de aviso.')); });
    return () => { activo = false; };
  }, [reintento, setErrorCarga]);

  useEffect(() => {
    let activo = true;
    setCargando(true);
    inventarioService.listarAlertas({ estado, page, pageSize: PAGE_SIZE })
      .then((r) => { if (activo) setPagina(r); })
      .catch((e) => { if (activo) setErrorCarga(mensajeError(e, 'No se pudieron cargar las alertas.')); })
      .finally(() => { if (activo) setCargando(false); });
    return () => { activo = false; };
  }, [estado, page, recarga, reintento, setErrorCarga]);

  async function cerrar(alerta, nuevoEstado) {
    setOcupado(alerta.id);
    try {
      await inventarioService.cambiarEstadoAlerta(alerta.id, nuevoEstado);
      setRecarga(recarga + 1);
    } catch (e) {
      setError(mensajeError(e));
    } finally { setOcupado(null); }
  }

  async function guardarDias(evento) {
    evento.preventDefault();
    setOcupado('dias');
    setErrorModal('');
    try {
      setParametros(await inventarioService.guardarParametros({
        diasProyeccionInventario: aNumero(editandoDias.proyeccion),
        diasAvisoVencimientoInventario: aNumero(editandoDias.vencimiento),
      }));
      setEditandoDias(null);
      setRecarga(recarga + 1);
    } catch (e) {
      setErrorModal(mensajeError(e));
    } finally { setOcupado(null); }
  }

  return (
    <PaginaInventario titulo="Alertas" descripcion="Avisos de stock bajo, de lo que se acabará pronto, de lo que vence y de diferencias.">
      <AvisoError mensaje={error} onCerrar={() => setError('')} />
      <AvisoError mensaje={errorCarga} onReintentar={reintentar} />
      <div className="inv-segmentado" role="group" aria-label="Estado de las alertas">
        {Object.entries(ESTADOS_ALERTA).map(([v, t]) => (
          <button key={v} type="button" className={estado === v ? 'active' : ''} aria-pressed={estado === v} onClick={() => { setEstado(v); setPage(1); }}>{t === 'Abierta' ? 'Abiertas' : `${t}s`}</button>
        ))}
      </div>
      <div className="panel">
        <div className="panel-heading"><div><h2>{estado === 'Abierta' ? 'Por atender' : `Alertas ${ESTADOS_ALERTA[estado].toLowerCase()}s`}</h2><p>Una entrada que repone el stock cierra sola las alertas de stock bajo y de quiebre.</p></div><span className="count-badge">{pagina.total}</span></div>
        {parametros && (
          <p className="field-hint inv-ajustes">
            Avisa un quiebre si el stock no alcanza para los próximos <strong>{parametros.diasProyeccionInventario} días</strong> y un vencimiento <strong>{parametros.diasAvisoVencimientoInventario} días</strong> antes.
            {puedeConfigurar && <button type="button" className="text-link" onClick={() => { setErrorModal(''); setEditandoDias({ proyeccion: String(parametros.diasProyeccionInventario), vencimiento: String(parametros.diasAvisoVencimientoInventario) }); }}>Cambiar</button>}
          </p>
        )}
        {cargando ? <p className="table-message"><span className="button-spinner" /> Cargando…</p>
          : !pagina.items.length ? <div className="empty-state"><span className="empty-icon"><Icon name="check" size={21} /></span><strong>No hay alertas aquí</strong></div>
            : (
              <ul className="inv-alertas">
                {pagina.items.map((a) => (
                  <li key={a.id} className={`inv-alerta inv-alerta-${a.tipo}`}>
                    <span className="inv-alerta-icono"><Icon name="alert" size={18} /></span>
                    <div className="inv-alerta-cuerpo">
                      <strong>{TIPOS_ALERTA[a.tipo] ?? a.tipo}: {a.insumo}</strong>
                      <p>
                        Stock ahora: {numero(a.stockInsumo)} {a.unidad} (reorden {numero(a.puntoReorden)} {a.unidad}).
                        {a.fechaQuiebreEstimada && <> Quedará por debajo del reorden el <strong>{fecha(a.fechaQuiebreEstimada)}</strong>.</>}
                        {a.detalle && <> {a.detalle}</>}
                      </p>
                      <small>Generada {fechaHora(a.fechaGeneracion)}{a.atendidaPor && <> · {ESTADOS_ALERTA[a.estado]} por {a.atendidaPor} el {fechaHora(a.fechaAtencion)}</>}</small>
                    </div>
                    {a.estado === 'Abierta' && (
                      <div className="inv-alerta-acciones">
                        <button type="button" className="button button-primary button-small" disabled={ocupado === a.id} onClick={() => cerrar(a, 'Atendida')} aria-label={`Marcar como atendida: ${TIPOS_ALERTA[a.tipo] ?? a.tipo} de ${a.insumo}`}>Atendida</button>
                        <button type="button" className="button button-secondary button-small" disabled={ocupado === a.id} onClick={() => cerrar(a, 'Descartada')} aria-label={`Descartar: ${TIPOS_ALERTA[a.tipo] ?? a.tipo} de ${a.insumo}`}>Descartar</button>
                      </div>
                    )}
                  </li>
                ))}
              </ul>
            )}
        <Paginacion page={page} pageSize={PAGE_SIZE} total={pagina.total} cargando={cargando} onCambiar={setPage} />
      </div>

      {editandoDias && (
        <Modal title="Días de aviso" eyebrow="GIRA · INVENTARIO" onClose={() => ocupado !== 'dias' && setEditandoDias(null)}>
          <form onSubmit={guardarDias} className="inv-form">
            <AvisoError mensaje={errorModal} />
            <label className="form-field"><span>Días de proyección para avisar un quiebre (1 a 14) <i>*</i></span>
              <input type="number" min={1} max={14} required value={editandoDias.proyeccion} onChange={(e) => setEditandoDias({ ...editandoDias, proyeccion: e.target.value })} /></label>
            <label className="form-field"><span>Días antes del vencimiento de un lote (1 a 30) <i>*</i></span>
              <input type="number" min={1} max={30} required value={editandoDias.vencimiento} onChange={(e) => setEditandoDias({ ...editandoDias, vencimiento: e.target.value })} /></label>
            <p className="field-hint">El cambio se usa desde la próxima revisión de alertas.</p>
            <div className="modal-actions">
              <button type="button" className="button button-secondary" onClick={() => setEditandoDias(null)} disabled={ocupado === 'dias'}>Cancelar</button>
              <button type="submit" className="button button-primary" disabled={ocupado === 'dias'}>{ocupado === 'dias' ? 'Guardando…' : 'Guardar'}</button>
            </div>
          </form>
        </Modal>
      )}
    </PaginaInventario>
  );
}
