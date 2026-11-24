import { useEffect, useState } from 'react';
import { personalService } from '../personalService.js';
import { aEnvioEmpleado, etiquetaEstado, mensajeDeError, validarEmpleado } from '../personalFormato.js';
import '../personal.css';
import PersonalTabs from '../components/PersonalTabs.jsx';

const FORM_VACIO = {
  nombre: '', apellido: '', cedula: '', telefono: '', email: '',
  cargoId: '', fechaIngreso: '', horasMaximasSemana: '40',
};

// Empleados (GI-362, RF-PER-01): listar, registrar, editar y activar o desactivar.
export default function EmpleadosPage() {
  const [empleados, setEmpleados] = useState([]);
  const [cargos, setCargos] = useState([]);
  const [soloActivos, setSoloActivos] = useState(false);
  const [form, setForm] = useState(FORM_VACIO);
  const [editandoId, setEditandoId] = useState(null);
  const [errores, setErrores] = useState({});
  const [mensaje, setMensaje] = useState('');
  const [cargando, setCargando] = useState(true);

  async function cargar() {
    setCargando(true);
    try {
      const [listaEmpleados, listaCargos] = await Promise.all([
        personalService.listarEmpleados({ soloActivos }),
        personalService.listarCargos(),
      ]);
      setEmpleados(listaEmpleados);
      setCargos(listaCargos);
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudieron cargar los empleados.'));
    } finally {
      setCargando(false);
    }
  }

  useEffect(() => { cargar(); }, [soloActivos]);

  function editar(empleado) {
    setEditandoId(empleado.id);
    setForm({
      nombre: empleado.nombre,
      apellido: empleado.apellido,
      cedula: empleado.cedula,
      telefono: empleado.telefono ?? '',
      email: empleado.email ?? '',
      cargoId: empleado.cargoId,
      fechaIngreso: empleado.fechaIngreso,
      horasMaximasSemana: String(empleado.horasMaximasSemana),
    });
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
    const erroresForm = validarEmpleado(form);
    setErrores(erroresForm);
    if (Object.keys(erroresForm).length > 0) return;

    try {
      const datos = aEnvioEmpleado(form);
      if (editandoId) await personalService.actualizarEmpleado(editandoId, datos);
      else await personalService.crearEmpleado(datos);
      setMensaje(editandoId ? 'Empleado actualizado.' : 'Empleado registrado.');
      limpiar();
      await cargar();
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudo guardar el empleado.'));
    }
  }

  async function cambiarEstado(empleado) {
    try {
      await personalService.cambiarEstadoEmpleado(empleado.id, !empleado.activo);
      setMensaje(empleado.activo ? 'Empleado desactivado.' : 'Empleado activado.');
      await cargar();
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudo cambiar el estado.'));
    }
  }

  const nombreDeCargo = (id) => cargos.find((c) => c.id === id)?.nombre ?? '—';

  return (
    <section className="page-section enter personal-page">
      <div className="page-heading-row">
        <div>
          <span className="eyebrow">PERSONAL Y TURNOS</span>
          <h1>Empleados</h1>
          <p>Registra al personal del restaurante y mantén al día su cargo y su estado.</p>
        </div>
      </div>

      <PersonalTabs />

      {mensaje && <p className="form-success" role="status">{mensaje}</p>}

      <div className="panel">
        <div className="panel-heading">
          <div>
            <h2>
              {editandoId ? 'Editar empleado' : 'Nuevo empleado'}
              {editandoId && <span className="etiqueta-edicion" role="status">Editando</span>}
            </h2>
            <p>La cédula debe ser única entre los empleados vigentes.</p>
          </div>
        </div>
        <form className="personal-form" onSubmit={guardar} noValidate>
          <div className="form-grid">
            <label className="form-field">
              <span>Nombre <i>*</i></span>
              <input value={form.nombre} onChange={(e) => setForm({ ...form, nombre: e.target.value })} placeholder="Ej. Ana" maxLength={100} />
            </label>
            <label className="form-field">
              <span>Apellido <i>*</i></span>
              <input value={form.apellido} onChange={(e) => setForm({ ...form, apellido: e.target.value })} placeholder="Ej. Pérez" maxLength={100} />
            </label>
            <label className="form-field">
              <span>Cédula <i>*</i></span>
              <input value={form.cedula} onChange={(e) => setForm({ ...form, cedula: e.target.value.replace(/\D/g, '').slice(0, 11) })} inputMode="numeric" maxLength={11} />
            </label>
            <label className="form-field">
              <span>Teléfono</span>
              <input value={form.telefono} onChange={(e) => setForm({ ...form, telefono: e.target.value })} maxLength={20} />
            </label>
            <label className="form-field">
              <span>Correo</span>
              <input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} maxLength={256} />
            </label>
            <label className="form-field">
              <span>Fecha de ingreso <i>*</i></span>
              <input type="date" value={form.fechaIngreso} onChange={(e) => setForm({ ...form, fechaIngreso: e.target.value })} />
            </label>
            <label className="form-field">
              <span>Horas máximas por semana <i>*</i></span>
              <input type="number" min="1" max="168" value={form.horasMaximasSemana} onChange={(e) => setForm({ ...form, horasMaximasSemana: e.target.value })} />
            </label>
            <div className="form-field">
              <span>Cargo <i>*</i></span>
              <div className="select-wrap">
                <select value={form.cargoId} onChange={(e) => setForm({ ...form, cargoId: e.target.value })}>
                  <option value="">Elige un cargo</option>
                  {cargos.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
                </select>
              </div>
            </div>
          </div>
          {Object.values(errores).map((texto) => <p key={texto} className="inline-error">{texto}</p>)}

          <div className="form-actions">
            {editandoId && <button type="button" className="button button-secondary" onClick={limpiar}>Cancelar edición</button>}
            <button type="submit" className="button button-primary">{editandoId ? 'Guardar cambios' : 'Registrar empleado'}</button>
          </div>
        </form>
      </div>

      <div className="panel users-panel">
        <div className="panel-heading">
          <div>
            <h2>Directorio del personal</h2>
            <p>Desactiva a un empleado en vez de eliminarlo; deja de aparecer para asignar turnos.</p>
          </div>
          <label className="field-hint">
            <input type="checkbox" checked={soloActivos} onChange={(e) => setSoloActivos(e.target.checked)} /> Mostrar solo activos
          </label>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead><tr><th>Nombre</th><th>Cédula</th><th>Cargo</th><th>Estado</th><th /></tr></thead>
            <tbody>
              {cargando ? (
                <tr><td colSpan="5" className="table-message"><span className="button-spinner" /> Cargando empleados…</td></tr>
              ) : empleados.length ? empleados.map((e) => (
                <tr key={e.id}>
                  <td><strong>{e.nombre} {e.apellido}</strong></td>
                  <td>{e.cedula}</td>
                  <td>{nombreDeCargo(e.cargoId)}</td>
                  <td><span className={`status-pill ${e.activo ? 'active' : 'inactive'}`}><span className="status-dot" />{etiquetaEstado(e.activo)}</span></td>
                  <td>
                    <div className="row-actions">
                      <button type="button" className="button button-secondary button-small" onClick={() => editar(e)}>Editar</button>
                      <button type="button" className="button button-secondary button-small" onClick={() => cambiarEstado(e)}>{e.activo ? 'Desactivar' : 'Activar'}</button>
                    </div>
                  </td>
                </tr>
              )) : (
                <tr><td colSpan="5"><div className="empty-state"><strong>No hay empleados para mostrar</strong><p>Registra uno con el formulario de arriba.</p></div></td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
