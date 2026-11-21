import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { authService } from '../authService.js';
import { useAuth } from '../AuthContext.jsx';
import Icon from '../components/Icon.jsx';
import Modal from '../components/Modal.jsx';

const pageSize = 8;

export default function Users() {
  const { user: currentUser } = useAuth();
  const [users, setUsers] = useState([]);
  const [roles, setRoles] = useState([]);
  const [rolesError, setRolesError] = useState('');
  const [role, setRole] = useState('');
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [confirmUser, setConfirmUser] = useState(null);
  const [busy, setBusy] = useState(false);
  const [reloadToken, setReloadToken] = useState(0);
  const [requests, setRequests] = useState([]);
  const [requestsError, setRequestsError] = useState('');
  const [showAllRequests, setShowAllRequests] = useState(false);
  const [resolvedNotice, setResolvedNotice] = useState(false);
  const [resetTarget, setResetTarget] = useState(null);
  const [resetResult, setResetResult] = useState(null);
  const [copied, setCopied] = useState(false);
  const [discardTarget, setDiscardTarget] = useState(null);

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
    authService.getUsers({ page, pageSize, role, status }).then((result) => {
      if (active) {
        setUsers(result.items);
        setTotal(result.totalCount);
      }
    }).catch((requestError) => {
      if (active) setError(requestError.response?.data?.detail || 'No se pudo cargar el listado. Intenta nuevamente.');
    }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [page, role, status, reloadToken]);
  useEffect(() => {
    let active = true;
    authService.getPendingRequests().then((result) => {
      if (active) setRequests(result);
    }).catch(() => {
      if (active) setRequestsError('No se pudieron cargar las solicitudes de contraseña.');
    });
    return () => { active = false; };
  }, []);

  function updateFilter(setter, value) { setter(value); setPage(1); }
  async function toggleStatus() {
    if (!confirmUser) return;
    setBusy(true);
    try {
      const nextActive = !confirmUser.activo;
      await authService.setUserStatus(confirmUser.id, nextActive);
      const nextTotal = status && ((status === 'active') !== nextActive) ? Math.max(0, total - 1) : total;
      const nextPage = Math.min(page, Math.max(1, Math.ceil(nextTotal / pageSize)));
      const refreshed = await authService.getUsers({ page: nextPage, pageSize, role, status });
      setUsers(refreshed.items);
      setTotal(refreshed.totalCount);
      if (nextPage !== page) setPage(nextPage);
      setConfirmUser(null);
    } catch (requestError) {
      setError(requestError.response?.data?.detail || 'No se pudo actualizar el estado o refrescar el listado de usuarios.');
      setConfirmUser(null);
    } finally { setBusy(false); }
  }
  async function refreshRequests() {
    try {
      const result = await authService.getPendingRequests();
      setRequests(result);
      setRequestsError('');
      if (!result.length) {
        setResolvedNotice(true);
        setTimeout(() => setResolvedNotice(false), 4000);
      }
    } catch {
      setRequests([]);
      setRequestsError('No se pudieron cargar las solicitudes de contraseña.');
    }
    window.dispatchEvent(new Event('gira:solicitudes-actualizadas'));
  }
  function openResetFromRow(user) {
    const request = requests.find((item) => item.usuarioId === user.id);
    setResetTarget({ id: user.id, nombre: user.nombre, activo: user.activo, fechaSolicitud: request?.fechaSolicitud });
  }
  function openResetFromRequest(request) {
    const listed = users.find((item) => item.id === request.usuarioId);
    setResetTarget({ id: request.usuarioId, nombre: request.nombreCompleto, activo: listed ? listed.activo : true, fechaSolicitud: request.fechaSolicitud });
  }
  async function resetPassword() {
    if (!resetTarget) return;
    setBusy(true);
    try {
      const result = await authService.resetUserPassword(resetTarget.id);
      setCopied(false);
      setResetResult({ nombre: resetTarget.nombre, temporaryPassword: result.temporaryPassword });
      const hadRequest = Boolean(resetTarget.fechaSolicitud);
      setResetTarget(null);
      if (hadRequest) await refreshRequests();
    } catch (requestError) {
      setError(requestError.response?.data?.detail || requestError.message || 'No se pudo generar la contraseña temporal. Intenta nuevamente.');
      setResetTarget(null);
    } finally { setBusy(false); }
  }
  function closeResetResult() {
    setResetResult(null);
    setCopied(false);
    setReloadToken(reloadToken + 1);
  }
  function copyPassword(password) {
    navigator.clipboard.writeText(password);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }
  async function discardRequest() {
    if (!discardTarget) return;
    setBusy(true);
    try {
      await authService.dismissRequest(discardTarget.usuarioId);
      setDiscardTarget(null);
      await refreshRequests();
    } catch (requestError) {
      setError(requestError.response?.data?.detail || requestError.message || 'No se pudo descartar la solicitud. Intenta nuevamente.');
      setDiscardTarget(null);
    } finally { setBusy(false); }
  }
  const pages = Math.max(1, Math.ceil(total / pageSize));

  return (
    <section className="page-section enter">
      <div className="page-heading-row"><div><span className="eyebrow">SEGURIDAD Y ACCESO</span><h1>Usuarios</h1><p>Gestiona las cuentas y permisos de tu equipo.</p></div><Link className="button button-primary" to="/seguridad/usuarios/nuevo"><Icon name="plus" size={17} /> Crear usuario</Link></div>
      {(error || rolesError || requestsError) && <div className="notice-error" role="alert"><span>!</span>{error || rolesError || requestsError}<button type="button" className="notice-dismiss" onClick={() => { setError(''); setRolesError(''); setRequestsError(''); }}><Icon name="close" size={16} /></button></div>}
      <div className="stats-strip">
        <div className="stat-card"><span className="stat-symbol violet"><Icon name="users" size={18} /></span><div><span className="stat-label">USUARIOS REGISTRADOS</span><strong>{total}</strong></div></div>
        <div className="stat-card"><span className="stat-symbol green"><Icon name="shield" size={18} /></span><div><span className="stat-label">ESTADO DEL SISTEMA</span><strong className="stat-status"><span className="status-dot" /> Protegido</strong></div></div>
        <div className="stat-card stat-hint"><span className="stat-symbol amber"><Icon name="key" size={18} /></span><div><span className="stat-label">ACCESO POR ROLES</span><strong className="stat-caption">Permisos controlados</strong></div></div>
      </div>
      {requests.length > 0 && (
        <div className="panel requests-panel" aria-live="polite">
          <div className="panel-heading"><div><h2>Solicitudes de contraseña</h2><p>Personas que pidieron ayuda para recuperar su acceso.</p></div><span className="count-badge">{requests.length} {requests.length === 1 ? 'pendiente' : 'pendientes'}</span></div>
          <div className="request-list">{(showAllRequests ? requests : requests.slice(0, 4)).map((request) => (
            <div className="request-row" key={request.usuarioId}>
              <span className="user-avatar">{request.nombreCompleto?.charAt(0) || 'U'}</span>
              <span className="user-name">{request.nombreCompleto}<small>{request.email}</small></span>
              <span className={`request-time${isLate(request.fechaSolicitud) ? ' request-late' : ''}`} title={isLate(request.fechaSolicitud) ? 'Solicitud sin atender' : undefined}><Icon name="clock" size={12} /> {relativeTime(request.fechaSolicitud)}</span>
              <span className="request-actions">
                <button type="button" className="button button-primary button-small" onClick={() => openResetFromRequest(request)} disabled={request.usuarioId === currentUser?.id} title={request.usuarioId === currentUser?.id ? 'Usa tu perfil para cambiar tu contraseña' : undefined}>Resetear contraseña</button>
                <button type="button" className="icon-button deactivate-action" onClick={() => setDiscardTarget(request)} title="Descartar solicitud" aria-label={`Descartar la solicitud de ${request.nombreCompleto}`}><Icon name="close" size={15} /></button>
              </span>
            </div>
          ))}</div>
          {requests.length > 4 && !showAllRequests && <div className="request-more"><button type="button" className="text-link" onClick={() => setShowAllRequests(true)}>Ver las {requests.length} solicitudes</button></div>}
        </div>
      )}
      {resolvedNotice && !requests.length && <div className="requests-resolved" role="status"><Icon name="check" size={16} /> Listo, no quedan solicitudes pendientes.</div>}
      <div className="panel users-panel">
        <div className="panel-heading"><div><h2>Directorio del equipo</h2><p>Busca y administra las cuentas del sistema.</p></div><span className="count-badge">{total} {total === 1 ? 'usuario' : 'usuarios'}</span></div>
        <div className="filter-row">
          <label className="select-wrap"><span className="sr-only">Filtrar por rol</span><select value={role} onChange={(event) => updateFilter(setRole, event.target.value)}><option value="">Todos los roles</option>{roles.map((item) => <option key={item}>{item}</option>)}</select></label>
          <label className="select-wrap"><span className="sr-only">Filtrar por estado</span><select value={status} onChange={(event) => updateFilter(setStatus, event.target.value)}><option value="">Todos los estados</option><option value="active">Activo</option><option value="inactive">Inactivo</option></select></label>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead><tr><th>USUARIO</th><th>ROL</th><th>ESTADO</th><th>ÚLTIMO ACCESO</th><th><span className="sr-only">Acciones</span></th></tr></thead>
            <tbody>{loading ? <tr><td colSpan="5" className="table-message"><span className="button-spinner" /> Cargando usuarios…</td></tr> : users.length ? users.map((user, index) => (
              <tr key={user.id} className="user-row" style={{ animationDelay: `${index * 35}ms` }}>
                <td><div className="user-cell"><span className={`user-avatar avatar-${index % 5}`}>{user.nombre?.charAt(0) || 'U'}</span><span className="user-name">{user.nombre}<small>{user.email}</small></span></div></td>
                <td><div className="role-tags">{user.roles.map((item) => <span className="role-tag" key={item}>{item}</span>)}</div></td>
                <td><span className={`status-pill ${user.activo ? 'active' : 'inactive'}`}><span className="status-dot" />{user.activo ? 'Activo' : 'Inactivo'}</span></td>
                <td className="date-cell">{user.ultimoAcceso ? new Date(user.ultimoAcceso).toLocaleDateString('es-DO', { day: '2-digit', month: 'short', year: 'numeric' }) : '—'}</td>
                <td><div className="row-actions"><Link className="icon-button" to={`/seguridad/usuarios/${user.id}`} title="Ver detalle" aria-label={`Ver detalle de ${user.nombre}`}><Icon name="eye" size={16} /></Link><Link className="icon-button" to={`/seguridad/usuarios/${user.id}/editar`} title="Editar" aria-label={`Editar ${user.nombre}`}><Icon name="more" size={17} /></Link><button type="button" className="icon-button reset-action" onClick={() => openResetFromRow(user)} disabled={user.id === currentUser?.id} title={user.id === currentUser?.id ? 'Usa tu perfil para cambiar tu contraseña' : 'Resetear contraseña'} aria-label={`Resetear contraseña de ${user.nombre}`}><Icon name="key" size={16} /></button><button type="button" className={`icon-button ${user.activo ? 'deactivate-action' : ''}`} onClick={() => setConfirmUser(user)} title={user.activo ? 'Desactivar' : 'Activar'} aria-label={`${user.activo ? 'Desactivar' : 'Activar'} ${user.nombre}`}><Icon name={user.activo ? 'lock' : 'check'} size={15} /></button></div></td>
              </tr>
            )) : <tr><td colSpan="5"><div className="empty-state"><span className="empty-icon"><Icon name="search" size={21} /></span><strong>No encontramos usuarios</strong><p>Prueba con otros filtros o crea una nueva cuenta.</p></div></td></tr>}</tbody>
          </table>
        </div>
        <div className="pagination"><span>Mostrando <strong>{total ? (page - 1) * pageSize + 1 : 0}–{Math.min(page * pageSize, total)}</strong> de <strong>{total}</strong></span><div className="pagination-actions"><button type="button" className="button button-secondary button-small" disabled={page <= 1 || loading} onClick={() => setPage(page - 1)}>Anterior</button><span className="page-number">{page} / {pages}</span><button type="button" className="button button-secondary button-small" disabled={page >= pages || loading} onClick={() => setPage(page + 1)}>Siguiente</button></div></div>
      </div>
      {confirmUser && <Modal title={`${confirmUser.activo ? 'Desactivar' : 'Activar'} usuario`} onClose={() => setConfirmUser(null)}><p className="modal-copy">¿Estás seguro de que deseas {confirmUser.activo ? 'desactivar' : 'activar'} a <strong>{confirmUser.nombre}</strong>? {confirmUser.activo ? 'Ya no podrá iniciar sesión mientras su cuenta esté inactiva.' : 'Podrá volver a iniciar sesión con su cuenta.'}</p><div className="modal-actions"><button className="button button-secondary" type="button" onClick={() => setConfirmUser(null)}>Cancelar</button><button className={`button ${confirmUser.activo ? 'button-danger' : 'button-primary'}`} type="button" onClick={toggleStatus} disabled={busy}>{busy ? 'Actualizando…' : confirmUser.activo ? 'Sí, desactivar' : 'Sí, activar'}</button></div></Modal>}
      {resetTarget && <Modal title="Resetear contraseña" onClose={() => setResetTarget(null)}><p className="modal-copy">¿Generar una contraseña temporal para <strong>{resetTarget.nombre}</strong>? Su contraseña actual dejará de funcionar de inmediato y se le pedirá crear una nueva al iniciar sesión.</p>{resetTarget.fechaSolicitud && <p className="modal-copy"><strong>{resetTarget.nombre}</strong> solicitó ayuda {relativeTime(resetTarget.fechaSolicitud)}. Al resetear, su solicitud se cierra.</p>}{!resetTarget.activo && <div className="required-notice"><Icon name="lock" size={16} /> Esta cuenta está inactiva: la contraseña nueva servirá cuando la vuelvas a activar.</div>}<div className="modal-actions"><button className="button button-secondary" type="button" onClick={() => setResetTarget(null)} disabled={busy}>Cancelar</button><button className="button button-primary" type="button" onClick={resetPassword} disabled={busy}>{busy ? 'Generando…' : 'Sí, resetear'}</button></div></Modal>}
      {resetResult && <Modal title="Contraseña reseteada" onClose={closeResetResult}><div className="created-user-info"><span className="created-success"><Icon name="check" size={20} /></span><p>Generamos una contraseña temporal para <strong>{resetResult.nombre}</strong>. Entrégasela en persona; no volverá a mostrarse.</p><label className="field-label" htmlFor="reset-temporary-password">CONTRASEÑA TEMPORAL</label><div className="temporary-password"><code id="reset-temporary-password">{resetResult.temporaryPassword}</code><button type="button" className={`icon-button${copied ? ' copy-done' : ''}`} onClick={() => copyPassword(resetResult.temporaryPassword)} title={copied ? 'Copiada' : 'Copiar contraseña'} aria-label={copied ? 'Contraseña copiada' : 'Copiar contraseña'}><Icon name={copied ? 'check' : 'key'} size={16} /></button></div>{copied ? <span className="form-success"><Icon name="check" size={14} /> Copiada al portapapeles</span> : <span className="field-hint">Se solicitará cambiarla en el primer inicio de sesión.</span>}</div><div className="modal-actions"><button className="button button-primary" type="button" onClick={closeResetResult}>Listo</button></div></Modal>}
      {discardTarget && <Modal title="Descartar solicitud" onClose={() => setDiscardTarget(null)}><p className="modal-copy">La solicitud de <strong>{discardTarget.nombreCompleto}</strong> desaparecerá de la lista y no se reseteará ninguna contraseña. Si todavía necesita ayuda, deberá volver a solicitarla.</p><div className="modal-actions"><button className="button button-secondary" type="button" onClick={() => setDiscardTarget(null)} disabled={busy}>Cancelar</button><button className="button button-danger" type="button" onClick={discardRequest} disabled={busy}>{busy ? 'Descartando…' : 'Sí, descartar'}</button></div></Modal>}
    </section>
  );
}

function minutesSince(value) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 0;
  return Math.max(0, Math.round((Date.now() - date.getTime()) / 60000));
}

function relativeTime(value) {
  const minutes = minutesSince(value);
  if (minutes < 60) return `hace ${Math.max(1, minutes)} min`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `hace ${hours} h`;
  const days = Math.floor(hours / 24);
  return days === 1 ? 'ayer' : `hace ${days} días`;
}

function isLate(value) {
  return minutesSince(value) >= 1440;
}
