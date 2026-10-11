import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { authService } from '../authService.js';
import Icon from '../components/Icon.jsx';
import Modal from '../components/Modal.jsx';
import RoleSelector from '../components/RoleSelector.jsx';

export default function UserForm() {
  const { userId } = useParams();
  const navigate = useNavigate();
  const editing = Boolean(userId);
  const [form, setForm] = useState({ nombre: '', email: '', roles: [] });
  const [loading, setLoading] = useState(editing);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [created, setCreated] = useState(null);

  useEffect(() => {
    if (!editing) return undefined;
    let active = true;
    authService.getUser(userId).then((user) => {
      if (!user) throw new Error('No se encontró la cuenta solicitada.');
      if (active) setForm({ nombre: user.nombre, email: user.email, roles: user.roles || [] });
    }).catch((requestError) => { if (active) setError(requestError.response?.data?.detail || requestError.message || 'No se pudo cargar el usuario.'); }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [editing, userId]);

  async function submit(event) {
    event.preventDefault();
    setError('');
    setSaving(true);
    try {
      const result = await authService.saveUser(form, userId);
      if (!editing && result.temporaryPassword) setCreated(result);
      else navigate('/seguridad/usuarios', { replace: true });
    } catch (requestError) {
      setError(requestError.response?.data?.detail || requestError.response?.data?.message || requestError.message || 'No se pudo guardar el usuario.');
    } finally { setSaving(false); }
  }

  if (loading) return <div className="loading-panel"><span className="button-spinner" /> Cargando usuario…</div>;
  return (
    <section className="page-section form-page enter">
      <Link to="/seguridad/usuarios" className="back-link"><Icon name="back" size={16} /> Volver a usuarios</Link>
      <div className="page-heading-row form-heading"><div><span className="eyebrow">GESTIÓN DE CUENTAS</span><h1>{editing ? 'Editar usuario' : 'Crear usuario'}</h1><p>{editing ? 'Actualiza los datos y permisos de esta cuenta.' : 'Registra un nuevo miembro del equipo en GIRA.'}</p></div></div>
      {error && <div className="notice-error" role="alert"><span>!</span>{error}</div>}
      <form className="panel user-form" onSubmit={submit}>
        <div className="form-section"><div className="form-section-title"><span className="form-step">01</span><div><h2>Información personal</h2><p>Datos de identificación para la cuenta.</p></div></div>
          <div className="form-grid"><label className="form-field"><span>Nombre completo <i>*</i></span><input autoComplete="name" value={form.nombre} onChange={(event) => setForm({ ...form, nombre: event.target.value })} placeholder="Ej. Ana Rodríguez" required minLength={2} maxLength={120} /></label><label className="form-field"><span>Correo electrónico <i>*</i></span><input type="email" autoComplete="email" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} placeholder="nombre@empresa.com" required maxLength={254} /></label></div>
        </div>
        <div className="form-section"><div className="form-section-title"><span className="form-step">02</span><div><h2>Roles y permisos</h2><p>Selecciona uno o más roles; sus descripciones te ayudan a elegir.</p></div></div><RoleSelector selected={form.roles} onChange={(roles) => setForm({ ...form, roles })} />{!form.roles.length && <span className="field-hint role-validation">Selecciona al menos un rol.</span>}</div>
        {!editing && <div className="form-tip"><span className="tip-icon"><Icon name="key" size={16} /></span><span><strong>Contraseña temporal</strong><small>Se generará al guardar y solo se mostrará una vez.</small></span></div>}
        <div className="form-actions"><Link className="button button-secondary" to="/seguridad/usuarios">Cancelar</Link><button type="submit" className="button button-primary" disabled={saving || !form.roles.length}>{saving ? <><span className="button-spinner" /> Guardando…</> : <>{editing ? 'Guardar cambios' : 'Crear usuario'} <Icon name="arrow" size={16} /></>}</button></div>
      </form>
      {created && <Modal title="Usuario creado" onClose={() => navigate('/seguridad/usuarios', { replace: true })}><div className="created-user-info"><span className="created-success"><Icon name="check" size={20} /></span><p>La cuenta de <strong>{created.user.nombre}</strong> está lista. Comparte esta contraseña temporal de forma segura; no volverá a mostrarse.</p><label className="field-label" htmlFor="temporary-password">CONTRASEÑA TEMPORAL</label><div className="temporary-password"><code id="temporary-password">{created.temporaryPassword}</code><button type="button" className="icon-button" onClick={() => navigator.clipboard.writeText(created.temporaryPassword)} title="Copiar contraseña" aria-label="Copiar contraseña"><Icon name="key" size={16} /></button></div><span className="field-hint">Se solicitará cambiarla en el primer inicio de sesión.</span></div><div className="modal-actions"><button className="button button-primary" type="button" onClick={() => navigate('/seguridad/usuarios', { replace: true })}>Listo</button></div></Modal>}
    </section>
  );
}
