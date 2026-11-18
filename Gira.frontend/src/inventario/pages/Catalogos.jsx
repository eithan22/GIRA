import { useEffect, useState } from 'react';
import Icon from '../../security/components/Icon.jsx';
import Modal from '../../security/components/Modal.jsx';
import PaginaInventario, { AvisoError, FilaEstado } from '../components/PaginaInventario.jsx';
import { mensajeError } from '../inventarioFormato.js';
import { inventarioService } from '../inventarioService.js';

// Cada catálogo: su ruta en la API, sus columnas y los campos del formulario.
const CATALOGOS = {
  'unidades-medida': { titulo: 'Unidades de medida', campos: [['nombre', 'Nombre', 50], ['abreviatura', 'Abreviatura', 10]] },
  'categorias-insumo': { titulo: 'Categorías de insumo', campos: [['nombre', 'Nombre', 60]] },
  'categorias-plato': { titulo: 'Categorías de plato', campos: [['nombre', 'Nombre', 60]] },
  proveedores: { titulo: 'Proveedores', campos: [['nombre', 'Nombre', 100], ['telefono', 'Teléfono (809, 829 u 849)', 14]], conActivo: true },
};

/** Ruta /inventario/catalogos: unidades, categorías y proveedores (RF-INV-02, RF-INV-13). Gerente y Administrador. */
export default function Catalogos() {
  const [recurso, setRecurso] = useState('unidades-medida');
  const [filas, setFilas] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState('');
  const [recarga, setRecarga] = useState(0);
  const [editando, setEditando] = useState(null);
  const [ocupado, setOcupado] = useState(false);
  const [errorModal, setErrorModal] = useState('');
  const def = CATALOGOS[recurso];

  useEffect(() => {
    let activo = true;
    setCargando(true);
    inventarioService.listarCatalogo(recurso)
      .then((r) => { if (activo) setFilas(r); })
      .catch((e) => { if (activo) setError(mensajeError(e, 'No se pudo cargar el catálogo.')); })
      .finally(() => { if (activo) setCargando(false); });
    return () => { activo = false; };
  }, [recurso, recarga]);

  function abrir(fila) {
    setErrorModal('');
    const vacio = Object.fromEntries(def.campos.map(([c]) => [c, '']));
    setEditando({ id: fila?.id, form: fila ? { ...vacio, ...fila } : { ...vacio, activo: true } });
  }

  async function guardar(evento) {
    evento.preventDefault();
    setOcupado(true);
    try {
      const cuerpo = { ...editando.form };
      delete cuerpo.id;
      if (editando.id) await inventarioService.actualizarCatalogo(recurso, editando.id, cuerpo);
      else await inventarioService.crearCatalogo(recurso, cuerpo);
      setEditando(null);
      setRecarga(recarga + 1);
    } catch (e) {
      setErrorModal(mensajeError(e));
    } finally { setOcupado(false); }
  }

  const columnas = def.campos.length + (def.conActivo ? 1 : 0) + 1;

  return (
    <PaginaInventario
      titulo="Catálogos"
      descripcion="Las listas que usan los insumos y los platos."
      accion={<button type="button" className="button button-primary" onClick={() => abrir(null)}><Icon name="plus" size={17} /> Agregar</button>}
    >
      <AvisoError mensaje={error} onCerrar={() => setError('')} />
      <div className="inv-segmentado" role="group" aria-label="Catálogo">
        {Object.entries(CATALOGOS).map(([clave, c]) => (
          <button key={clave} type="button" className={recurso === clave ? 'active' : ''} aria-pressed={recurso === clave} onClick={() => setRecurso(clave)}>{c.titulo}</button>
        ))}
      </div>
      <div className="panel">
        <div className="panel-heading"><div><h2>{def.titulo}</h2><p>Los nombres no se pueden repetir.</p></div><span className="count-badge">{filas.length}</span></div>
        <div className="table-scroll">
          <table className="data-table">
            <thead><tr>{def.campos.map(([c, t]) => <th key={c}>{t.toUpperCase()}</th>)}{def.conActivo && <th>ESTADO</th>}<th><span className="sr-only">Acciones</span></th></tr></thead>
            <tbody>
              {cargando || !filas.length
                ? <FilaEstado columnas={columnas} cargando={cargando} vacio="Todavía no hay registros" />
                : filas.map((f) => (
                  <tr key={f.id} className={f.activo === false ? 'inv-inactivo' : undefined}>
                    {def.campos.map(([c]) => <td key={c}>{f[c]}</td>)}
                    {def.conActivo && <td><span className={`status-pill ${f.activo ? 'active' : 'inactive'}`}><span className="status-dot" />{f.activo ? 'Activo' : 'Inactivo'}</span></td>}
                    <td><div className="row-actions"><button type="button" className="icon-button" onClick={() => abrir(f)} title="Editar" aria-label={`Editar ${f.nombre}`}><Icon name="edit" size={16} /></button></div></td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      </div>

      {editando && (
        <Modal title={`${editando.id ? 'Editar' : 'Agregar'}: ${def.titulo.toLowerCase()}`} eyebrow="GIRA · INVENTARIO" onClose={() => !ocupado && setEditando(null)}>
          <form onSubmit={guardar} className="inv-form">
            <AvisoError mensaje={errorModal} />
            {def.campos.map(([c, t, max]) => (
              <label key={c} className="form-field"><span>{t} <i>*</i></span><input value={editando.form[c] ?? ''} maxLength={max} required onChange={(e) => setEditando({ ...editando, form: { ...editando.form, [c]: e.target.value } })} /></label>
            ))}
            {def.conActivo && <label className="inv-check"><input type="checkbox" checked={editando.form.activo} onChange={(e) => setEditando({ ...editando, form: { ...editando.form, activo: e.target.checked } })} /> Activo</label>}
            <div className="modal-actions">
              <button type="button" className="button button-secondary" onClick={() => setEditando(null)} disabled={ocupado}>Cancelar</button>
              <button type="submit" className="button button-primary" disabled={ocupado}>{ocupado ? 'Guardando…' : 'Guardar'}</button>
            </div>
          </form>
        </Modal>
      )}
    </PaginaInventario>
  );
}
