import { useEffect, useState } from 'react';
import { personalService } from '../personalService.js';
import { aEnvioRequerimiento, mensajeDeError, validarRequerimiento } from '../personalFormato.js';
import PersonalTabs from '../components/PersonalTabs.jsx';
import '../personal.css';

// Requerimiento de personal por cargo (GI-366, RF-PER-08): ver y editar comensales por empleado y mínimo por franja.
export default function RequerimientosPage() {
  const [cargos, setCargos] = useState([]);
  const [filas, setFilas] = useState({});
  const [errores, setErrores] = useState({});
  const [guardando, setGuardando] = useState(null);
  const [mensaje, setMensaje] = useState('');
  const [cargando, setCargando] = useState(true);

  async function cargar() {
    setCargando(true);
    try {
      const [listaCargos, requerimientos] = await Promise.all([
        personalService.listarCargos(),
        personalService.listarRequerimientos(),
      ]);
      const porCargo = Object.fromEntries(requerimientos.map((r) => [r.cargoId, r]));
      setCargos(listaCargos);
      setFilas(Object.fromEntries(listaCargos.map((c) => [c.id, {
        comensalesPorEmpleado: porCargo[c.id]?.comensalesPorEmpleado ?? '',
        minimoPorFranja: porCargo[c.id]?.minimoPorFranja ?? '',
        configurado: Boolean(porCargo[c.id]),
      }])));
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudieron cargar los requerimientos.'));
    } finally {
      setCargando(false);
    }
  }

  useEffect(() => { cargar(); }, []);

  function cambiar(cargoId, campo, valor) {
    setFilas((prev) => ({ ...prev, [cargoId]: { ...prev[cargoId], [campo]: valor } }));
  }

  async function guardar(cargoId) {
    const fila = filas[cargoId];
    const erroresFila = validarRequerimiento({ cargoId, ...fila });
    setErrores((prev) => ({ ...prev, [cargoId]: erroresFila }));
    if (Object.keys(erroresFila).length > 0) return;

    setGuardando(cargoId);
    try {
      await personalService.guardarRequerimiento(aEnvioRequerimiento({ cargoId, ...fila }));
      setMensaje('Requerimiento guardado.');
      await cargar();
    } catch (error) {
      setMensaje(mensajeDeError(error, 'No se pudo guardar el requerimiento.'));
    } finally {
      setGuardando(null);
    }
  }

  return (
    <section className="page-section enter personal-page">
      <div className="page-heading-row">
        <div>
          <span className="eyebrow">PERSONAL Y TURNOS</span>
          <h1>Requerimientos de personal</h1>
          <p>Cuántos comensales atiende un empleado de cada cargo y el mínimo por franja.</p>
        </div>
      </div>

      <PersonalTabs />

      {mensaje && <p className="form-success" role="status">{mensaje}</p>}

      <div className="panel users-panel">
        <div className="panel-heading">
          <div>
            <h2>Requerimiento por cargo</h2>
            <p>Un solo requerimiento por cargo. Los valores deben ser mayores que cero.</p>
          </div>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead><tr><th>Cargo</th><th>Comensales por empleado</th><th>Mínimo por franja</th><th /></tr></thead>
            <tbody>
              {cargando ? (
                <tr><td colSpan="4" className="table-message"><span className="button-spinner" /> Cargando requerimientos…</td></tr>
              ) : cargos.length ? cargos.map((cargo) => {
                const fila = filas[cargo.id] ?? {};
                const erroresFila = errores[cargo.id] ?? {};
                return (
                  <tr key={cargo.id}>
                    <td><strong>{cargo.nombre}</strong></td>
                    <td>
                      <input type="number" min="1" value={fila.comensalesPorEmpleado ?? ''} onChange={(e) => cambiar(cargo.id, 'comensalesPorEmpleado', e.target.value)} aria-label={`Comensales por empleado de ${cargo.nombre}`} />
                      {erroresFila.comensalesPorEmpleado && <p className="inline-error">{erroresFila.comensalesPorEmpleado}</p>}
                    </td>
                    <td>
                      <input type="number" min="1" value={fila.minimoPorFranja ?? ''} onChange={(e) => cambiar(cargo.id, 'minimoPorFranja', e.target.value)} aria-label={`Mínimo por franja de ${cargo.nombre}`} />
                      {erroresFila.minimoPorFranja && <p className="inline-error">{erroresFila.minimoPorFranja}</p>}
                    </td>
                    <td>
                      <div className="row-actions">
                        <span className={`status-pill ${fila.configurado ? 'active' : 'inactive'}`}><span className="status-dot" />{fila.configurado ? 'Configurado' : 'Sin configurar'}</span>
                        <button type="button" className="button button-primary button-small" disabled={guardando === cargo.id} onClick={() => guardar(cargo.id)}>
                          {guardando === cargo.id ? 'Guardando…' : 'Guardar'}
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              }) : (
                <tr><td colSpan="4"><div className="empty-state"><strong>Aún no hay cargos</strong><p>Crea primero los cargos en la pestaña Cargos.</p></div></td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
