import { useEffect, useState } from 'react';
import { useAuth } from '../../security/AuthContext.jsx';
import Icon from '../../security/components/Icon.jsx';
import Modal from '../../security/components/Modal.jsx';
import PaginaInventario, { AvisoError, FilaEstado, Paginacion } from '../components/PaginaInventario.jsx';
import { NIVELES, ROLES_GESTIONAR, aNumero, dinero, mensajeError, numero, tieneRol } from '../inventarioFormato.js';
import { inventarioService } from '../inventarioService.js';
import { useCarga } from '../useCarga.js';

const PAGE_SIZE = 20;
const VACIO = { nombre: '', categoriaInsumoId: '', unidadMedidaId: '', puntoReorden: '', costoUnitario: '', perecedero: false, proveedorId: '' };

/** Ruta /inventario/insumos: catálogo con stock y nivel (RF-INV-01). Gerente y Administrador lo editan. */
export default function Insumos() {
  const { user } = useAuth();
  const puedeGestionar = tieneRol(user, ROLES_GESTIONAR);
  const [filtros, setFiltros] = useState({ buscar: '', categoria: '', nivel: '', incluirInactivos: false });
  const [buscarTexto, setBuscarTexto] = useState('');
  const [page, setPage] = useState(1);
  const [pagina, setPagina] = useState({ items: [], total: 0 });
  const [resumen, setResumen] = useState(null);
  const [catalogos, setCatalogos] = useState({ categorias: [], unidades: [], proveedores: [] });
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState('');
  const [recarga, setRecarga] = useState(0);
  const [editando, setEditando] = useState(null); // { id?, form }
  const [cambiarEstado, setCambiarEstado] = useState(null);
  const [ocupado, setOcupado] = useState(false);
  const [errorModal, setErrorModal] = useState('');
  const { errorCarga, setErrorCarga, reintento, reintentar } = useCarga();

  useEffect(() => {
    let activo = true;
    Promise.all([
      inventarioService.listarCatalogo('categorias-insumo'),
      inventarioService.listarCatalogo('unidades-medida'),
      puedeGestionar ? inventarioService.listarCatalogo('proveedores') : Promise.resolve([]),
    ]).then(([categorias, unidades, proveedores]) => {
      if (activo) setCatalogos({ categorias, unidades, proveedores });
    }).catch((e) => { if (activo) setErrorCarga(mensajeError(e, 'No se pudieron cargar las categorías y unidades.')); });
    return () => { activo = false; };
  }, [puedeGestionar, reintento, setErrorCarga]);

  useEffect(() => {
    let activo = true;
    setCargando(true);
    Promise.all([
      inventarioService.listarInsumos({ ...filtros, page, pageSize: PAGE_SIZE }),
      inventarioService.resumenInsumos(),
    ]).then(([lista, totales]) => {
      if (!activo) return;
      setPagina(lista);
      setResumen(totales);
    }).catch((e) => { if (activo) setErrorCarga(mensajeError(e, 'No se pudo cargar el inventario.')); })
      .finally(() => { if (activo) setCargando(false); });
    return () => { activo = false; };
  }, [filtros, page, recarga, reintento, setErrorCarga]);

  function filtrar(cambio) { setFiltros({ ...filtros, ...cambio }); setPage(1); }

  function abrir(insumo) {
    setErrorModal('');
    setEditando(insumo
      ? { id: insumo.id, form: { ...VACIO, ...insumo, puntoReorden: String(insumo.puntoReorden), costoUnitario: String(insumo.costoUnitario), proveedorId: insumo.proveedorId ?? '' } }
      : { form: { ...VACIO, categoriaInsumoId: catalogos.categorias[0]?.id ?? '', unidadMedidaId: catalogos.unidades[0]?.id ?? '' } });
  }

  async function guardar(evento) {
    evento.preventDefault();
    const f = editando.form;
    const cuerpo = {
      nombre: f.nombre,
      categoriaInsumoId: f.categoriaInsumoId,
      unidadMedidaId: f.unidadMedidaId,
      puntoReorden: aNumero(f.puntoReorden) ?? -1,
      costoUnitario: aNumero(f.costoUnitario) ?? -1,
      perecedero: f.perecedero,
      proveedorId: f.proveedorId || null,
    };
    setOcupado(true);
    try {
      if (editando.id) await inventarioService.actualizarInsumo(editando.id, cuerpo);
      else await inventarioService.crearInsumo(cuerpo);
      setEditando(null);
      setRecarga(recarga + 1);
    } catch (e) {
      setErrorModal(mensajeError(e));
    } finally { setOcupado(false); }
  }

  async function confirmarEstado() {
    setOcupado(true);
    try {
      await inventarioService.cambiarEstadoInsumo(cambiarEstado.id, !cambiarEstado.activo);
      setRecarga(recarga + 1);
    } catch (e) {
      setError(mensajeError(e));
    } finally { setOcupado(false); setCambiarEstado(null); }
  }

  const campo = (nombre) => ({
    value: editando.form[nombre],
    onChange: (e) => setEditando({ ...editando, form: { ...editando.form, [nombre]: e.target.type === 'checkbox' ? e.target.checked : e.target.value } }),
  });

  return (
    <PaginaInventario
      titulo="Insumos"
      descripcion="Lo que hay en la cocina y el almacén. El stock solo cambia con movimientos."
      accion={puedeGestionar && <button type="button" className="button button-primary" onClick={() => abrir(null)}><Icon name="plus" size={17} /> Nuevo insumo</button>}
    >
      <AvisoError mensaje={error} onCerrar={() => setError('')} />
      <AvisoError mensaje={errorCarga} onReintentar={reintentar} />
      <div className="stats-strip">
        <div className="stat-card"><span className="stat-symbol violet"><Icon name="box" size={18} /></span><div><span className="stat-label">INSUMOS ACTIVOS</span><strong>{resumen?.activos ?? '—'}</strong></div></div>
        <button type="button" className="stat-card inv-stat-boton" onClick={() => filtrar({ nivel: 'Bajo' })}><span className="stat-symbol amber"><Icon name="alert" size={18} /></span><div><span className="stat-label">EN NIVEL BAJO</span><strong>{resumen?.bajos ?? '—'}</strong></div></button>
        <button type="button" className="stat-card inv-stat-boton" onClick={() => filtrar({ nivel: 'Critico' })}><span className="stat-symbol inv-rojo"><Icon name="alert" size={18} /></span><div><span className="stat-label">EN NIVEL CRÍTICO</span><strong>{resumen?.criticos ?? '—'}</strong></div></button>
      </div>
      <div className="panel">
        <div className="panel-heading"><div><h2>Catálogo de insumos</h2><p>Bajo: en o por debajo del punto de reorden. Crítico: en o por debajo de la mitad.</p></div><span className="count-badge">{pagina.total} {pagina.total === 1 ? 'insumo' : 'insumos'}</span></div>
        <form className="filter-row inv-filtros" onSubmit={(e) => { e.preventDefault(); filtrar({ buscar: buscarTexto.trim() }); }}>
          <label className="inv-buscar"><span className="sr-only">Buscar por nombre</span><input type="search" value={buscarTexto} onChange={(e) => setBuscarTexto(e.target.value)} placeholder="Buscar por nombre" /></label>
          <label className="select-wrap"><span className="sr-only">Categoría</span><select value={filtros.categoria} onChange={(e) => filtrar({ categoria: e.target.value })}><option value="">Todas las categorías</option>{catalogos.categorias.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}</select></label>
          <label className="select-wrap"><span className="sr-only">Nivel</span><select value={filtros.nivel} onChange={(e) => filtrar({ nivel: e.target.value })}><option value="">Todos los niveles</option>{Object.entries(NIVELES).map(([v, t]) => <option key={v} value={v}>{t}</option>)}</select></label>
          <label className="inv-check"><input type="checkbox" checked={filtros.incluirInactivos} onChange={(e) => filtrar({ incluirInactivos: e.target.checked })} /> Ver inactivos</label>
          <button type="submit" className="button button-secondary button-small">Buscar</button>
        </form>
        <div className="table-scroll">
          <table className="data-table">
            <thead><tr><th>INSUMO</th><th>CATEGORÍA</th><th>STOCK</th><th>REORDEN</th><th>NIVEL</th><th>COSTO</th><th>PROVEEDOR</th><th>ESTADO</th>{puedeGestionar && <th><span className="sr-only">Acciones</span></th>}</tr></thead>
            <tbody>
              {cargando || !pagina.items.length
                ? <FilaEstado columnas={puedeGestionar ? 9 : 8} cargando={cargando} vacio="No hay insumos con esos filtros" />
                : pagina.items.map((i) => (
                  <tr key={i.id} className={i.activo ? undefined : 'inv-inactivo'}>
                    <td><strong className="inv-nombre">{i.nombre}</strong>{i.perecedero && <small className="inv-sub">Perecedero</small>}</td>
                    <td>{i.categoria}</td>
                    <td className="inv-num">{numero(i.cantidadStock)} {i.unidad}</td>
                    <td className="inv-num">{numero(i.puntoReorden)} {i.unidad}</td>
                    <td><span className={`inv-nivel inv-nivel-${i.nivel}`}>{NIVELES[i.nivel] ?? i.nivel}</span></td>
                    <td className="inv-num">{dinero(i.costoUnitario)}</td>
                    <td>{i.proveedor ?? '—'}</td>
                    <td><span className={`status-pill ${i.activo ? 'active' : 'inactive'}`}><span className="status-dot" />{i.activo ? 'Activo' : 'Inactivo'}</span></td>
                    {puedeGestionar && (
                      <td><div className="row-actions">
                        <button type="button" className="icon-button" onClick={() => abrir(i)} title="Editar" aria-label={`Editar ${i.nombre}`}><Icon name="edit" size={16} /></button>
                        <button type="button" className={`icon-button${i.activo ? ' deactivate-action' : ''}`} onClick={() => setCambiarEstado(i)} title={i.activo ? 'Desactivar' : 'Activar'} aria-label={`${i.activo ? 'Desactivar' : 'Activar'} ${i.nombre}`}><Icon name={i.activo ? 'lock' : 'check'} size={15} /></button>
                      </div></td>
                    )}
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
        <Paginacion page={page} pageSize={PAGE_SIZE} total={pagina.total} cargando={cargando} onCambiar={setPage} />
      </div>

      {editando && (
        <Modal title={editando.id ? 'Editar insumo' : 'Nuevo insumo'} eyebrow="GIRA · INVENTARIO" onClose={() => !ocupado && setEditando(null)} wide>
          <form onSubmit={guardar} className="inv-form">
            <AvisoError mensaje={errorModal} />
            <label className="form-field"><span>Nombre <i>*</i></span><input {...campo('nombre')} required maxLength={100} /></label>
            <div className="form-grid">
              <label className="form-field"><span>Categoría <i>*</i></span><select {...campo('categoriaInsumoId')} required>{catalogos.categorias.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}</select></label>
              <label className="form-field"><span>Unidad de medida <i>*</i></span><select {...campo('unidadMedidaId')} required>{catalogos.unidades.map((u) => <option key={u.id} value={u.id}>{u.nombre} ({u.abreviatura})</option>)}</select></label>
              <label className="form-field"><span>Punto de reorden <i>*</i></span><input {...campo('puntoReorden')} inputMode="decimal" required /></label>
              <label className="form-field"><span>Costo por unidad (RD$) <i>*</i></span><input {...campo('costoUnitario')} inputMode="decimal" required /></label>
            </div>
            <label className="form-field"><span>Proveedor</span><select {...campo('proveedorId')}><option value="">Sin proveedor</option>{catalogos.proveedores.filter((p) => p.activo || p.id === editando.form.proveedorId).map((p) => <option key={p.id} value={p.id}>{p.nombre}</option>)}</select></label>
            <label className="inv-check"><input type="checkbox" checked={editando.form.perecedero} onChange={campo('perecedero').onChange} /> Es perecedero (pide fecha de vencimiento en cada entrada)</label>
            {!editando.id && <p className="field-hint">El insumo empieza con stock 0. Para cargar lo que ya hay, registra una entrada en Movimientos.</p>}
            <div className="modal-actions">
              <button type="button" className="button button-secondary" onClick={() => setEditando(null)} disabled={ocupado}>Cancelar</button>
              <button type="submit" className="button button-primary" disabled={ocupado}>{ocupado ? 'Guardando…' : 'Guardar'}</button>
            </div>
          </form>
        </Modal>
      )}

      {cambiarEstado && (
        <Modal title={`${cambiarEstado.activo ? 'Desactivar' : 'Activar'} insumo`} eyebrow="GIRA · INVENTARIO" onClose={() => !ocupado && setCambiarEstado(null)}>
          <p className="modal-copy">
            {cambiarEstado.activo
              ? <>¿Desactivar <strong>{cambiarEstado.nombre}</strong>? Ya no se podrán registrar movimientos ni usarlo en recetas. Si una receta activa lo usa, no se podrá.</>
              : <>¿Activar <strong>{cambiarEstado.nombre}</strong> otra vez?</>}
          </p>
          <div className="modal-actions">
            <button type="button" className="button button-secondary" onClick={() => setCambiarEstado(null)} disabled={ocupado}>Cancelar</button>
            <button type="button" className={`button ${cambiarEstado.activo ? 'button-danger' : 'button-primary'}`} onClick={confirmarEstado} disabled={ocupado}>{ocupado ? 'Guardando…' : cambiarEstado.activo ? 'Sí, desactivar' : 'Sí, activar'}</button>
          </div>
        </Modal>
      )}
    </PaginaInventario>
  );
}
