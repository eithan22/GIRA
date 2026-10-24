import { useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { useAuth } from '../AuthContext.jsx';
import { authService } from '../authService.js';
import Icon from '../components/Icon.jsx';

export default function Register() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({ nombreCompleto: '', email: '', password: '', confirmacion: '' });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const passwordPolicy = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/.test(form.password);

  if (user) return <Navigate to={user.roles?.includes('Administrador') ? '/seguridad/usuarios' : '/inicio'} replace />;

  async function submit(event) {
    event.preventDefault();
    setError('');
    if (!passwordPolicy) {
      setError('La contraseña debe tener al menos 8 caracteres, una mayúscula, una minúscula, un número y un símbolo.');
      return;
    }
    if (form.password !== form.confirmacion) {
      setError('La contraseña y su confirmación no coinciden.');
      return;
    }

    setLoading(true);
    try {
      await authService.register({
        nombreCompleto: form.nombreCompleto.trim(),
        email: form.email.trim(),
        password: form.password,
      });
      navigate('/login', {
        replace: true,
        state: { notice: 'Cuenta creada. Ya puedes iniciar sesión con tu correo y contraseña.' },
      });
    } catch (requestError) {
      const data = requestError.response?.data;
      setError(data?.detail || data?.message || data?.title || 'No se pudo crear la cuenta. Revisa los datos e inténtalo de nuevo.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="login-main register-main">
      <section className="login-form-wrap enter">
        <Link to="/login" className="brand register-brand"><span className="brand-mark">G</span><span className="brand-name">GIRA<span>.</span></span></Link>
        <div className="login-heading">
          <span className="eyebrow">CREA TU CUENTA</span>
          <h1>Regístrate</h1>
          <p>Completa tus datos para crear una cuenta de personal.</p>
        </div>
        <form className="login-form" onSubmit={submit}>
          <label className="field-label" htmlFor="nombre-completo">Nombre completo</label>
          <div className="input-wrap"><input id="nombre-completo" type="text" autoComplete="name" placeholder="Tu nombre" value={form.nombreCompleto} onChange={(event) => setForm({ ...form, nombreCompleto: event.target.value })} required minLength={2} maxLength={120} /></div>
          <label className="field-label" htmlFor="registro-email">Correo electrónico</label>
          <div className="input-wrap"><input id="registro-email" type="email" autoComplete="email" placeholder="nombre@empresa.com" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} required maxLength={256} /></div>
          <label className="field-label" htmlFor="registro-password">Contraseña</label>
          <div className="input-wrap"><Icon className="input-leading-icon" name="lock" size={16} /><input id="registro-password" type="password" autoComplete="new-password" placeholder="Crea una contraseña segura" value={form.password} onChange={(event) => setForm({ ...form, password: event.target.value })} required minLength={8} maxLength={128} /></div>
          <label className="field-label" htmlFor="registro-confirmacion">Confirmar contraseña</label>
          <div className="input-wrap"><Icon className="input-leading-icon" name="lock" size={16} /><input id="registro-confirmacion" type="password" autoComplete="new-password" placeholder="Escribe nuevamente la contraseña" value={form.confirmacion} onChange={(event) => setForm({ ...form, confirmacion: event.target.value })} required minLength={8} maxLength={128} /></div>
          <p className="register-role-note">La cuenta se crea con acceso básico de Mesero.</p>
          {error && <div className="form-error" role="alert"><span>!</span>{error}</div>}
          <button className="button button-primary login-submit" type="submit" disabled={loading}>{loading ? <><span className="button-spinner" /> Creando cuenta…</> : <>Crear cuenta <Icon name="arrow" size={16} /></>}</button>
        </form>
        <p className="login-register-link">¿Ya tienes cuenta? <Link to="/login">Inicia sesión</Link></p>
      </section>
    </main>
  );
}
