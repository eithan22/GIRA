import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { authService } from '../authService.js';
import Icon from '../components/Icon.jsx';
import AccessHistory from '../components/AccessHistory.jsx';

export default function UserDetail() {
  const { userId } = useParams();
  const [user, setUser] = useState(null);
  const [accesses, setAccesses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    Promise.all([authService.getUser(userId), authService.getAccesses(userId)]).then(([result, history]) => {
      if (active) {
        if (!result) throw new Error('No se encontró el usuario solicitado.');
        setUser(result);
        setAccesses(history);
      }
    }).catch((requestError) => { if (active) setError(requestError.response?.data?.detail || requestError.message || 'No se pudo cargar el detalle del usuario.'); }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [userId]);

  if (loading) return <div className="loading-panel"><span className="button-spinner" /> Cargando detalle…</div>;
  if (!user || error) return <section className="page-section"><div className="notice-error">{error || 'No se encontró el usuario.'}</div><Link to="/seguridad/usuarios" className="back-link"><Icon name="back" size={16} /> Volver a usuarios</Link></section>;

  return (
    <section className="page-section enter">
      <Link to="/seguridad/usuarios" className="back-link"><Icon name="back" size={16} /> Volver a usuarios</Link>
      <div className="detail-hero panel"><span className="detail-avatar">{user.nombre.charAt(0)}</span><div className="detail-user"><span className="eyebrow">PERFIL DE USUARIO</span><h1>{user.nombre}</h1><span>{user.email}</span></div><span className={`status-pill ${user.activo ? 'active' : 'inactive'}`}><span className="status-dot" />{user.activo ? 'Activo' : 'Inactivo'}</span><Link className="button button-secondary" to={`/seguridad/usuarios/${userId}/editar`}>Editar perfil</Link></div>
      <div className="detail-grid">
        <article className="panel detail-card"><div className="panel-heading"><div><h2>Roles asignados</h2><p>Permisos vinculados a esta cuenta.</p></div><span className="stat-symbol violet"><Icon name="shield" size={17} /></span></div><div className="detail-roles">{user.roles.map((role) => <span className="role-tag large-role" key={role}>{role}</span>)}</div><div className="detail-meta"><span>Último acceso</span><strong>{user.ultimoAcceso ? new Date(user.ultimoAcceso).toLocaleDateString('es-DO', { dateStyle: 'long' }) : '—'}</strong></div></article>
        <article className="panel detail-card access-card"><div className="panel-heading"><div><h2>Últimos accesos</h2><p>Los intentos fallidos aparecen resaltados.</p></div><span className="count-badge">{accesses.length} registros</span></div><AccessHistory accesses={accesses} /></article>
      </div>
    </section>
  );
}
