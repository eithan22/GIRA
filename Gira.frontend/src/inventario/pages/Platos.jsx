import { useEffect, useState } from 'react';
import { useAuth } from '../../security/AuthContext.jsx';
import Icon from '../../security/components/Icon.jsx';
import Modal from '../../security/components/Modal.jsx';
import PaginaInventario, { AvisoError, FilaEstado } from '../components/PaginaInventario.jsx';
import { ROLES_GESTIONAR, aNumero, dinero, mensajeError, numero, tieneRol } from '../inventarioFormato.js';
import { inventarioService, todosLosInsumos } from '../inventarioService.js';
import { useCarga } from '../useCarga.js';

const VACIO = { nombre: '', categoriaPlatoId: '', precio: '', porcentajeDemanda: '', activo: true };

/**
 * Ruta /inventario/platos: platos con su % de demanda (RN-INV-05) y sus recetas (RF-INV-03, RF-INV-04).
 * Cocina las ve; Gerente y Administrador las editan.
 */
export default function Platos() {
  const { user } = useAuth();
  const puedeGestionar = tieneRol(user, ROLES_GESTIONAR);
  const [listado, setListado] = useState({ items: [], sumaDemandaActivos: 0, advertencia: null });
  const [categorias, setCategorias] = useState([]);
  const [insumos, setInsumos] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState('');
  const [recarga, setRecarga] = useState(0);
  const [editando, setEditando] = useState(null);
  const [receta, setReceta] = useState(null); // { plato, form: { porciones, instrucciones, lineas: [{ insumoId, cantidad }] } }
  const [ocupado, setOcupado] = useState(false);
  const [errorModal, setErrorModal] = useState('');
  const { errorCarga, setErrorCarga, reintento, reintentar } = useCarga();

  useEffect(() => {
    let activo = true;
    Promise.all([inventarioService.listarCatalogo('categorias-plato'), todosLosInsumos()])
      .then(([cats, ins]) => { if (activo) { setCategorias(cats); setInsumos(ins); } })
      .catch((e) => { if (activo) setErrorCarga(mensajeError(e, 'No se pudieron cargar las categorías o los insumos.')); });
    return () => { activo = false; };
  }, [reintento, setErrorCarga]);

  useEffect(() => {
    let activo = true;
    setCargando(true);
    inventarioService.listarPlatos()
      .then((r) => { if (activo) setListado(r); })
      .catch((e) => { if (activo) setErrorCarga(mensajeError(e, 'No se pudieron cargar los platos.')); })
      .finally(() => { if (activo) setCargando(false); });
    return () => { activo = false; };
  }, [recarga, reintento, setErrorCarga]);

  function abrir(plato) {
    setErrorModal('');
    setEditando(plato
      ? { id: plato.id, form: { ...VACIO, ...plato, precio: String(plato.precio), porcentajeDemanda: String(plato.porcentajeDemanda) } }
      : { form: { ...VACIO, categoriaPlatoId: categorias[0]?.id ?? '' } });
  }

  async function guardar(evento) {
    evento.preventDefault();
    const f = editando.form;
    const cuerpo = { nombre: f.nombre, categoriaPlatoId: f.categoriaPlatoId, precio: aNumero(f.precio) ?? 0, porcentajeDemanda: aNumero(f.porcentajeDemanda) ?? -1, activo: f.activo };
    setOcupado(true);
    try {
      if (editando.id) await inventarioService.actualizarPlato(editando.id, cuerpo);
      else await inventarioService.crearPlato(cuerpo);
      setEditando(null);
      setRecarga(recarga + 1);
    } catch (e) {
      setErrorModal(mensajeError(e));
    } finally { setOcupado(false); }
  }

  async function abrirReceta(plato) {
    setErrorModal('');
    try {
      const r = await inventarioService.obtenerReceta(plato.id);
      setReceta({
        plato,
        lectura: r,
        form: {
          porciones: String(r.porciones ?? 1),
          instrucciones: r.instrucciones ?? '',
          lineas: r.lineas.map((l) => ({ insumoId: l.insumoId, cantidad: String(l.cantidad) })),
        },
      });
    } catch (e) {
      setError(mensajeError(e, 'No se pudo abrir la receta.'));
    }
  }

  function cambiarLinea(indice, cambio) {
    const lineas = receta.form.lineas.map((l, i) => (i === indice ? { ...l, ...cambio } : l));
    setReceta({ ...receta, form: { ...receta.form, lineas } });
  }

  async function guardarReceta(evento) {
    evento.preventDefault();
    const f = receta.form;
    setOcupado(true);
    try {
      await inventarioService.guardarReceta(receta.plato.id, {
        porciones: Math.trunc(aNumero(f.porciones) ?? 0),
        instrucciones: f.instrucciones,
        lineas: f.lineas.filter((l) => l.insumoId).map((l) => ({ insumoId: l.insumoId, cantidad: aNumero(l.cantidad) ?? 0 })),
      });
      setReceta(null);
      setRecarga(recarga + 1);
    } catch (e) {
      setErrorModal(mensajeError(e));
    } finally { setOcupado(false); }
  }

  const campo = (nombre) => ({
    value: editando.form[nombre],
    onChange: (e) => setEditando({ ...editando, form: { ...editando.form, [nombre]: e.target.type === 'checkbox' ? e.target.checked : e.target.value } }),
  });
  const insumosActivos = insumos.filter((i) => i.activo);

  return (
    <PaginaInventario
      titulo="Platos y recetas"
      descripcion="Qué parte de los clientes pide cada plato y qué lleva. Con esto se calcula el consumo."
      accion={puedeGestionar && <button type="button" className="button button-primary" onClick={() => abrir(null)}><Icon name="plus" size={17} /> Nuevo plato</button>}
    >
      <AvisoError mensaje={error} onCerrar={() => setError('')} />
      <AvisoError mensaje={errorCarga} onReintentar={reintentar} />
      {/* Sin datos (cargando o con error) no se puede decir si la suma es confiable. */}
      {!cargando && !errorCarga && <div className={`inv-demanda${listado.advertencia ? ' inv-demanda-aviso' : ''}`} role="status">
        <Icon name={listado.advertencia ? 'alert' : 'check'} size={16} />
        <span>Suma del % de demanda de los platos activos: <strong>{numero(listado.sumaDemandaActivos, 2)} %</strong>. {listado.advertencia ?? 'Está entre 95 % y 105 %: la proyección es confiable.'}</span>
      </div>}
      <div className="panel">
        <div className="panel-heading"><div><h2>Platos</h2><p>Los activos primero. Un plato sin receta no cuenta para el consumo.</p></div><span className="count-badge">{listado.items.length} platos</span></div>
        <div className="table-scroll">
          <table className="data-table">
            <thead><tr><th>PLATO</th><th>CATEGORÍA</th><th>PRECIO</th><th>% DEMANDA</th><th>RECETA</th><th>ESTADO</th><th><span className="sr-only">Acciones</span></th></tr></thead>
            <tbody>
              {cargando || !listado.items.length
                ? <FilaEstado columnas={7} cargando={cargando} vacio="Todavía no hay platos" />
                : listado.items.map((p) => (
                  <tr key={p.id} className={p.activo ? undefined : 'inv-inactivo'}>
                    <td><strong className="inv-nombre">{p.nombre}</strong></td>
                    <td>{p.categoria}</td>
                    <td className="inv-num">{dinero(p.precio)}</td>
                    <td className="inv-num">{numero(p.porcentajeDemanda, 2)} %</td>
                    <td>{p.lineasReceta ? `${p.lineasReceta} ${p.lineasReceta === 1 ? 'insumo' : 'insumos'}` : <span className="inv-nivel inv-nivel-Bajo">Sin receta</span>}</td>
                    <td><span className={`status-pill ${p.activo ? 'active' : 'inactive'}`}><span className="status-dot" />{p.activo ? 'Activo' : 'Inactivo'}</span></td>
                    <td><div className="row-actions">
                      <button type="button" className="button button-secondary button-small" onClick={() => abrirReceta(p)} aria-label={`${puedeGestionar ? 'Receta' : 'Ver receta'} de ${p.nombre}`}>{puedeGestionar ? 'Receta' : 'Ver receta'}</button>
                      {puedeGestionar && <button type="button" className="icon-button" onClick={() => abrir(p)} title="Editar" aria-label={`Editar ${p.nombre}`}><Icon name="edit" size={16} /></button>}
                    </div></td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      </div>

      {editando && (
        <Modal title={editando.id ? 'Editar plato' : 'Nuevo plato'} eyebrow="GIRA · INVENTARIO" onClose={() => !ocupado && setEditando(null)} wide>
          <form onSubmit={guardar} className="inv-form">
            <AvisoError mensaje={errorModal} />
            <label className="form-field"><span>Nombre <i>*</i></span><input {...campo('nombre')} required maxLength={100} /></label>
            <div className="form-grid">
              <label className="form-field"><span>Categoría <i>*</i></span><select {...campo('categoriaPlatoId')} required>{categorias.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}</select></label>
              <label className="form-field"><span>Precio (RD$) <i>*</i></span><input {...campo('precio')} inputMode="decimal" required /></label>
              <label className="form-field"><span>% de demanda <i>*</i></span><input {...campo('porcentajeDemanda')} inputMode="decimal" required /><small className="field-hint">De cada 100 clientes, cuántos lo piden.</small></label>
            </div>
            <label className="inv-check"><input type="checkbox" checked={editando.form.activo} onChange={campo('activo').onChange} /> Activo (está en el menú)</label>
            <div className="modal-actions">
              <button type="button" className="button button-secondary" onClick={() => setEditando(null)} disabled={ocupado}>Cancelar</button>
              <button type="submit" className="button button-primary" disabled={ocupado}>{ocupado ? 'Guardando…' : 'Guardar'}</button>
            </div>
          </form>
        </Modal>
      )}

      {receta && (
        <Modal title={`Receta: ${receta.plato.nombre}`} eyebrow="GIRA · INVENTARIO" onClose={() => !ocupado && setReceta(null)} wide>
          {puedeGestionar ? (
            <form onSubmit={guardarReceta} className="inv-form">
              <AvisoError mensaje={errorModal} />
              <label className="form-field"><span>¿Para cuántas porciones? <i>*</i></span><input value={receta.form.porciones} onChange={(e) => setReceta({ ...receta, form: { ...receta.form, porciones: e.target.value } })} inputMode="numeric" required /></label>
              <fieldset className="inv-lineas">
                <legend className="field-label">INSUMOS (CANTIDAD PARA TODAS LAS PORCIONES)</legend>
                {receta.form.lineas.map((l, i) => {
                  // Un insumo ya inactivo no está en la lista de activos: se muestra con el nombre que trae la receta.
                  const guardado = receta.lectura.lineas.find((x) => x.insumoId === l.insumoId);
                  const insumo = insumos.find((x) => x.id === l.insumoId);
                  return (
                    <div className="inv-linea" key={i}>
                      <label className="form-field"><span className="sr-only">Insumo</span>
                        <select value={l.insumoId} onChange={(e) => cambiarLinea(i, { insumoId: e.target.value })} required>
                          <option value="">Elige un insumo</option>
                          {insumosActivos.map((x) => <option key={x.id} value={x.id}>{x.nombre}</option>)}
                          {guardado && !insumo && <option value={guardado.insumoId}>{guardado.insumo} (inactivo)</option>}
                        </select>
                      </label>
                      <label className="form-field"><span className="sr-only">Cantidad</span><input value={l.cantidad} onChange={(e) => cambiarLinea(i, { cantidad: e.target.value })} inputMode="decimal" placeholder="Cantidad" required /></label>
                      <span className="inv-unidad">{insumo?.unidad ?? guardado?.unidad ?? ''}</span>
                      <button type="button" className="icon-button deactivate-action" onClick={() => setReceta({ ...receta, form: { ...receta.form, lineas: receta.form.lineas.filter((_, j) => j !== i) } })} aria-label="Quitar insumo"><Icon name="close" size={15} /></button>
                    </div>
                  );
                })}
                <button type="button" className="text-link" onClick={() => setReceta({ ...receta, form: { ...receta.form, lineas: [...receta.form.lineas, { insumoId: '', cantidad: '' }] } })}>+ Agregar insumo</button>
              </fieldset>
              <label className="form-field"><span>Instrucciones</span><textarea rows={3} maxLength={2000} value={receta.form.instrucciones} onChange={(e) => setReceta({ ...receta, form: { ...receta.form, instrucciones: e.target.value } })} /></label>
              <div className="modal-actions">
                <button type="button" className="button button-secondary" onClick={() => setReceta(null)} disabled={ocupado}>Cancelar</button>
                <button type="submit" className="button button-primary" disabled={ocupado}>{ocupado ? 'Guardando…' : 'Guardar receta'}</button>
              </div>
            </form>
          ) : (
            <div className="inv-form">
              {receta.lectura.porciones ? (
                <>
                  <p className="modal-copy">Rinde <strong>{receta.lectura.porciones}</strong> {receta.lectura.porciones === 1 ? 'porción' : 'porciones'}.</p>
                  <table className="data-table"><thead><tr><th>INSUMO</th><th>CANTIDAD</th><th>POR PORCIÓN</th></tr></thead>
                    <tbody>{receta.lectura.lineas.map((l) => <tr key={l.insumoId}><td>{l.insumo}</td><td className="inv-num">{numero(l.cantidad)} {l.unidad}</td><td className="inv-num">{numero(l.cantidadPorPorcion)} {l.unidad}</td></tr>)}</tbody>
                  </table>
                  {receta.lectura.instrucciones && <p className="modal-copy inv-instrucciones">{receta.lectura.instrucciones}</p>}
                </>
              ) : <p className="modal-copy">Este plato todavía no tiene receta.</p>}
              <div className="modal-actions"><button type="button" className="button button-primary" onClick={() => setReceta(null)}>Cerrar</button></div>
            </div>
          )}
        </Modal>
      )}
    </PaginaInventario>
  );
}
