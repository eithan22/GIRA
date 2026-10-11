import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { authService } from '../authService.js';
import Icon from '../components/Icon.jsx';
import Modal from '../components/Modal.jsx';

const pageSize = 8;

export default function Users() {
  const [users, setUsers] = useState([]);
  const [roles, setRoles] = useState([]);
  const [rolesError, setRolesError] = useState('');
  const [search, setSearch] = useState('');
  const [role, setRole] = useState('');
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [confirmUser, setConfirmUser] = useState(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let active = true;
    authService.getRoles().then((result) => {
      if (active) setRoles((Array.isArray(result) ? result : result.items ?? result.roles ?? []).map((item) => typeof item === 'string' ? item : item.name ?? item.nombre));
    }).catch((requestError) => { if (active) setRolesError(requestError.response?.data?.detail || 'No se pudieron cargar los roles de usuario.'); });
    return () => { active = false; };
  }, []);
  useEffect(() => {
    let active = true;
    setLoading(true);
    setError('');
    authService.getUsers({ page, pageSize, search, role, status }).then((result) => {
      if (active) {
        setUsers(result.items);
        setTotal(result.totalCount);
      }
    }).catch((requestError) => {
      if (active) setError(requestError.response?.data?.detail || 'No se pudo cargar el listado. Intenta nuevamente.');
    }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [page, search, role, status]);

  function updateFilter(setter, value) { setter(value); setPage(1); }
  async function toggleStatus() {
    if (!confirmUser) return;
    setBusy(true);
    try {
      const nextActive = !confirmUser.activo;
      await authService.setUserStatus(confirmUser.id, nextActive);
      const nextTotal = status && ((status === 'active') !== nextActive) ? Math.max(0, total - 1) : total;
      const nextPage = Math.min(page, Math.max(1, Math.ceil(nextTotal / pageSize)));
      const refreshed = await authService.getUsers({ page: nextPage, pageSize, search, role, status });
      setUsers(refreshed.items);
      setTotal(refreshed.totalCount);
      if (nextPage !== page) setPage(nextPage);
      setConfirmUser(null);
    } catch (requestError) {
      setError(requestError.response?.data?.detail || 'No se pudo actualizar el estado o refrescar el listado de usuarios.');
      setConfirmUser(null);
    } finally { setBusy(false); }
  }
  const pages = Math.max(1, Math.ceil(total / pageSize));

  return (
    <section className="page-section enter">
      <div className="page-heading-row"><div><span className="eyebrow">SEGURIDAD Y ACCESO</span><h1>Usuarios</h1><p>Gestiona las cuentas y permisos de tu equipo.</p></div><Link className="button button-primary" to="/seguridad/usuarios/nuevo"><Icon name="plus" size={17} /> Crear usuario</Link></div>
      {(error || rolesError) && <div className="notice-error" role="alert"><span>!</span>{error || rolesError}<button type="button" className="notice-dismiss" onClick={() => { setError(''); setRolesError(''); }}><Icon name="close" size={16} /></button></div>}
      <div className="stats-strip">
        <div className="stat-card"><span className="stat-symbol violet"><Icon name="users" size={18} /></span><div><span className="stat-label">USUARIOS REGISTRADOS</span><strong>{total}</strong></div></div>
        <div className="stat-card"><span className="stat-symbol green"><Icon name="shield" size={18} /></span><div><span className="stat-label">ESTADO DEL SISTEMA</span><strong className="stat-status"><span className="status-dot" /> Protegido</strong></div></div>
        <div className="stat-card stat-hint"><span className="stat-symbol amber"><Icon name="key" size={18} /></span><div><span className="stat-label">ACCESO POR ROLES</span><strong className="stat-caption">Permisos controlados</strong></div></div>
      </div>
      <div className="panel users-panel">
        <div className="panel-heading"><div><h2>Directorio del equipo</h2><p>Busca y administra las cuentas del sistema.</p></div><span className="count-badge">{total} {total === 1 ? 'usuario' : 'usuarios'}</span></div>
        <div className="filter-row">
          <div className="input-wrap search-input"><Icon name="search" size={17} /><input aria-label="Buscar usuarios" placeholder="Buscar por nombre o correo…" value={search} onChange={(event) => updateFilter(setSearch, event.target.value)} /></div>
          <label className="select-wrap"><span className="sr-only">Filtrar por rol</span><select value={role} onChange={(event) => updateFilter(setRole, event.target.value)}><option value="">Todos los roles</option>{roles.map((item) => <option key={item}>{item}</option>)}</select></label>
          <label className="select-wrap"><span className="sr-only">Filtrar por estado</span><select value={status} onChange={(event) => updateFilter(setStatus, event.target.value)}><option value="">Todos los estados</option><option value="active">Activo</option><option value="inactive">Inactivo</option></select></label>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead><tr><th>USUARIO</th><th>ROL</th><th>ESTADO</th><th>FECHA DE ALTA</th><th><span className="sr-only">Acciones</span></th></tr></thead>
            <tbody>{loading ? <tr><td colSpan="5" className="table-message"><span className="button-spinner" /> Cargando usuarios…</td></tr> : users.length ? users.map((user, index) => (
              <tr key={user.id} className="user-row" style={{ animationDelay: `${index * 35}ms` }}>
                <td><div className="user-cell"><span className={`user-avatar avatar-${index % 5}`}>{user.nombre?.charAt(0) || 'U'}</span><span className="user-name">{user.nombre}<small>{user.email}</small></span></div></td>
                <td><div className="role-tags">{user.roles.map((item) => <span className="role-tag" key={item}>{item}</span>)}</div></td>
                <td><span className={`status-pill ${user.activo ? 'active' : 'inactive'}`}><span className="status-dot" />{user.activo ? 'Activo' : 'Inactivo'}</span></td>
                <td className="date-cell">{user.createdAt ? new Date(user.createdAt).toLocaleDateString('es-DO', { day: '2-digit', month: 'short', year: 'numeric' }) : '—'}</td>
                <td><div className="row-actions"><Link className="icon-button" to={`/seguridad/usuarios/${user.id}`} title="Ver detalle" aria-label={`Ver detalle de ${user.nombre}`}><Icon name="eye" size={16} /></Link><Link className="icon-button" to={`/seguridad/usuarios/${user.id}/editar`} title="Editar" aria-label={`Editar ${user.nombre}`}><Icon name="more" size={17} /></Link><button type="button" className={`icon-button ${user.activo ? 'deactivate-action' : ''}`} onClick={() => setConfirmUser(user)} title={user.activo ? 'Desactivar' : 'Activar'} aria-label={`${user.activo ? 'Desactivar' : 'Activar'} ${user.nombre}`}><Icon name={user.activo ? 'lock' : 'check'} size={15} /></button></div></td>
              </tr>
            )) : <tr><td colSpan="5"><div className="empty-state"><span className="empty-icon"><Icon name="search" size={21} /></span><strong>No encontramos usuarios</strong><p>Prueba con otros filtros o crea una nueva cuenta.</p></div></td></tr>}</tbody>
          </table>
        </div>
        <div className="pagination"><span>Mostrando <strong>{total ? (page - 1) * pageSize + 1 : 0}–{Math.min(page * pageSize, total)}</strong> de <strong>{total}</strong></span><div className="pagination-actions"><button type="button" className="button button-secondary button-small" disabled={page <= 1 || loading} onClick={() => setPage(page - 1)}>Anterior</button><span className="page-number">{page} / {pages}</span><button type="button" className="button button-secondary button-small" disabled={page >= pages || loading} onClick={() => setPage(page + 1)}>Siguiente</button></div></div>
      </div>
      {confirmUser && <Modal title={`${confirmUser.activo ? 'Desactivar' : 'Activar'} usuario`} onClose={() => setConfirmUser(null)}><p className="modal-copy">¿Estás seguro de que deseas {confirmUser.activo ? 'desactivar' : 'activar'} a <strong>{confirmUser.nombre}</strong>? {confirmUser.activo ? 'Ya no podrá iniciar sesión mientras su cuenta esté inactiva.' : 'Podrá volver a iniciar sesión con su cuenta.'}</p><div className="modal-actions"><button className="button button-secondary" type="button" onClick={() => setConfirmUser(null)}>Cancelar</button><button className={`button ${confirmUser.activo ? 'button-danger' : 'button-primary'}`} type="button" onClick={toggleStatus} disabled={busy}>{busy ? 'Actualizando…' : confirmUser.activo ? 'Sí, desactivar' : 'Sí, activar'}</button></div></Modal>}
    </section>
  );
}
