import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { authService } from '../authService.js';
import { useAuth } from '../AuthContext.jsx';
import Icon from '../components/Icon.jsx';

export default function ChangePassword() {
  const { user, updateUser } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [form, setForm] = useState({ currentPassword: '', newPassword: '', confirmPassword: '' });
  const [show, setShow] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const forced = Boolean(user?.debeCambiarPassword || location.state?.forced);
  const meetsPolicy = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/.test(form.newPassword);

  async function submit(event) {
    event.preventDefault();
    setError('');
    setSuccess('');
    if (!meetsPolicy) return setError('Usa al menos 8 caracteres, con mayúscula, minúscula, número y símbolo.');
    if (form.newPassword !== form.confirmPassword) return setError('Las contraseñas nuevas no coinciden.');
    setLoading(true);
    try {
      const updated = await authService.changePassword(form);
      updateUser({ ...user, ...updated, debeCambiarPassword: false });
      setSuccess('Tu contraseña se actualizó correctamente.');
      if (forced) {
        const destination = location.state?.from?.pathname || (user.roles.includes('Administrador') ? '/seguridad/usuarios' : '/inicio');
        window.setTimeout(() => navigate(destination, { replace: true }), 700);
      } else {
        setForm({ currentPassword: '', newPassword: '', confirmPassword: '' });
      }
    } catch (requestError) {
      setError(requestError.response?.data?.detail || requestError.response?.data?.message || 'No se pudo actualizar la contraseña. Verifica la contraseña actual.');
    } finally { setLoading(false); }
  }

  return (
    <section className="page-section password-page enter">
      {!forced && <span className="eyebrow">SEGURIDAD DE LA CUENTA</span>}
      <div className="password-layout">
        <div className="password-intro"><span className="password-illustration"><Icon name="key" size={25} /></span><span className="eyebrow">{forced ? 'ACCIÓN REQUERIDA' : 'TU PERFIL'}</span><h1>{forced ? 'Crea una nueva contraseña.' : 'Cambia tu contraseña.'}</h1><p>{forced ? 'Por seguridad, debes actualizar tu contraseña temporal antes de continuar.' : 'Mantén tu cuenta segura con una contraseña única y difícil de adivinar.'}</p>{forced && <div className="required-notice"><Icon name="lock" size={16} /> El resto del sistema permanecerá bloqueado hasta completar este paso.</div>}</div>
        <form className="panel password-form" onSubmit={submit}>
          <h2>Actualiza tus credenciales</h2><p className="form-subtitle">Tu nueva contraseña debe cumplir con todos los requisitos.</p>
          <label className="form-field"><span>Contraseña actual <i>*</i></span><input type={show ? 'text' : 'password'} autoComplete="current-password" value={form.currentPassword} onChange={(event) => setForm({ ...form, currentPassword: event.target.value })} required /></label>
          <label className="form-field"><span>Nueva contraseña <i>*</i></span><input type={show ? 'text' : 'password'} autoComplete="new-password" value={form.newPassword} onChange={(event) => setForm({ ...form, newPassword: event.target.value })} required minLength={8} /></label>
          <label className="form-field"><span>Confirmar contraseña <i>*</i></span><input type={show ? 'text' : 'password'} autoComplete="new-password" value={form.confirmPassword} onChange={(event) => setForm({ ...form, confirmPassword: event.target.value })} required minLength={8} /></label>
          <button type="button" className="password-toggle" onClick={() => setShow(!show)}><Icon name="eye" size={15} /> {show ? 'Ocultar contraseñas' : 'Mostrar contraseñas'}</button>
          <ul className="password-rules"><li className={form.newPassword.length >= 8 ? 'rule-met' : ''}><span /> Al menos 8 caracteres</li><li className={/[A-Z]/.test(form.newPassword) ? 'rule-met' : ''}><span /> Una letra mayúscula y una minúscula</li><li className={/\d/.test(form.newPassword) ? 'rule-met' : ''}><span /> Un número</li><li className={/[^A-Za-z0-9]/.test(form.newPassword) ? 'rule-met' : ''}><span /> Un símbolo</li></ul>
          {error && <div className="form-error" role="alert"><span>!</span>{error}</div>}{success && <div className="form-success" role="status"><Icon name="check" size={16} /> {success}</div>}
          <button className="button button-primary password-submit" disabled={loading || !meetsPolicy || !form.currentPassword || !form.confirmPassword}>{loading ? <><span className="button-spinner" /> Guardando…</> : <>Actualizar contraseña <Icon name="arrow" size={16} /></>}</button>
        </form>
      </div>
    </section>
  );
}
