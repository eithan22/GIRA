import { useEffect, useState } from 'react';
import { personalService } from '../personalService.js';
import { aEnvioCargo, mensajeDeError, validarCargo } from '../personalFormato.js';
import '../personal.css';
import PersonalTabs from '../components/PersonalTabs.jsx';

const FORM_VACIO = { nombre: '', descripcion: '' };

// Catálogo de cargos (GI-362, RF-PER-02): listar, crear y editar.
export default function CargosPage() {
  const [cargos, setCargos] = useState([]);
  const [form, setForm] = useState(FORM_VACIO);
  const [editandoId, setEditandoId] = useState(null);
  const [errores, setErrores] = useState({});
  const [mensaje, setMensaje] = useState('');
  const [cargando, setCargando] = useState(true);

  async function cargar() {
    setCargando(true);
    try {
      setCargos(await personalService.listarCargos());
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudieron cargar los cargos.'));
    } finally {
      setCargando(false);
    }
  }

  useEffect(() => { cargar(); }, []);

  function editar(cargo) {
    setEditandoId(cargo.id);
    setForm({ nombre: cargo.nombre, descripcion: cargo.descripcion ?? '' });
    setErrores({});
    setMensaje('');
  }

  function limpiar() {
    setEditandoId(null);
    setForm(FORM_VACIO);
    setErrores({});
  }

  async function guardar(evento) {
    evento.preventDefault();
    const erroresForm = validarCargo(form);
    setErrores(erroresForm);
    if (Object.keys(erroresForm).length > 0) return;

    try {
      const datos = aEnvioCargo(form);
      if (editandoId) await personalService.actualizarCargo(editandoId, datos);
      else await personalService.crearCargo(datos);
      setMensaje(editandoId ? 'Cargo actualizado.' : 'Cargo creado.');
      limpiar();
      await cargar();
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudo guardar el cargo.'));
    }
  }

  return (
    <section className="page-section enter personal-page">
      <div className="page-heading-row">
        <div>
          <span className="eyebrow">PERSONAL Y TURNOS</span>
          <h1>Cargos</h1>
          <p>Define los cargos del personal, como mesero, cocinero o anfitrión.</p>
        </div>
      </div>

      <PersonalTabs />

      {mensaje && <p className="form-success" role="status">{mensaje}</p>}

      <div className="panel">
        <div className="panel-heading">
          <div>
            <h2>
              {editandoId ? 'Editar cargo' : 'Nuevo cargo'}
              {editandoId && <span className="etiqueta-edicion" role="status">Editando</span>}
            </h2>
            <p>Los nombres de cargo no se pueden repetir.</p>
          </div>
        </div>
        <form className="personal-form" onSubmit={guardar} noValidate>
          <div className="form-grid">
            <label className="form-field">
              <span>Nombre <i>*</i></span>
              <input value={form.nombre} onChange={(e) => setForm({ ...form, nombre: e.target.value })} placeholder="Ej. Mesero" maxLength={100} />
            </label>
            <label className="form-field">
              <span>Descripción</span>
              <input value={form.descripcion} onChange={(e) => setForm({ ...form, descripcion: e.target.value })} placeholder="Opcional" maxLength={250} />
            </label>
          </div>
          {errores.nombre && <p className="inline-error">{errores.nombre}</p>}
          {errores.descripcion && <p className="inline-error">{errores.descripcion}</p>}

          <div className="form-actions">
            {editandoId && <button type="button" className="button button-secondary" onClick={limpiar}>Cancelar edición</button>}
            <button type="submit" className="button button-primary">{editandoId ? 'Guardar cambios' : 'Crear cargo'}</button>
          </div>
        </form>
      </div>

      <div className="panel users-panel">
        <div className="panel-heading">
          <div>
            <h2>Catálogo de cargos</h2>
            <p>Edita un cargo para cambiar su nombre o descripción.</p>
          </div>
          <span className="count-badge">{cargos.length} {cargos.length === 1 ? 'cargo' : 'cargos'}</span>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead><tr><th>Nombre</th><th>Descripción</th><th /></tr></thead>
            <tbody>
              {cargando ? (
                <tr><td colSpan="3" className="table-message"><span className="button-spinner" /> Cargando cargos…</td></tr>
              ) : cargos.length ? cargos.map((cargo) => (
                <tr key={cargo.id}>
                  <td><strong>{cargo.nombre}</strong></td>
                  <td>{cargo.descripcion ?? '—'}</td>
                  <td>
                    <div className="row-actions">
                      <button type="button" className="button button-secondary button-small" onClick={() => editar(cargo)}>Editar</button>
                    </div>
                  </td>
                </tr>
              )) : (
                <tr><td colSpan="3"><div className="empty-state"><strong>Aún no hay cargos</strong><p>Crea el primero con el formulario de arriba.</p></div></td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
