import { useEffect, useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { authService } from '../authService.js';
import { useAuth } from '../AuthContext.jsx';
import Icon from '../components/Icon.jsx';

export default function ForgotPassword() {
  const { user } = useAuth();
  const [email, setEmail] = useState('');
  const [sent, setSent] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [cooldown, setCooldown] = useState(0);

  useEffect(() => {
    if (!cooldown) return undefined;
    const timer = setTimeout(() => setCooldown(cooldown - 1), 1000);
    return () => clearTimeout(timer);
  }, [cooldown]);

  if (user) return <Navigate to={defaultPath(user)} replace />;

  async function submit(event) {
    event.preventDefault();
    setError('');
    setLoading(true);
    try {
      await authService.forgotPassword(email.trim());
      setSent(true);
    } catch (requestError) {
      if (requestError.response?.status === 429) {
        setCooldown(60);
        setError('Ya enviaste una solicitud hace poco. Espera unos minutos antes de intentar de nuevo.');
      } else {
        setError('No pudimos enviar tu solicitud. Revisa tu conexión e intenta nuevamente.');
      }
    } finally { setLoading(false); }
  }

  return (
    <main className="login-screen">
      <section className="login-visual">
        <Link to="/login" className="brand login-brand"><span className="brand-mark">G</span><span className="brand-name">GIRA<span>.</span></span></Link>
        <div className="login-art">
          <div className="orbit orbit-one" /><div className="orbit orbit-two" />
          <div className="login-art-card"><div className="art-lock"><Icon name="key" size={31} /></div><span className="eyebrow">ACOMPAÑAMIENTO INTERNO</span><h2>Tu administrador<br />te devuelve el acceso.</h2><p>GIRA no envía correos. Un administrador del restaurante genera tu contraseña temporal y te la entrega en persona.</p><div className="art-lines"><span /><span /><span /></div></div>
          <span className="floating-chip chip-one"><span className="status-dot" /> Sin correos externos</span><span className="floating-chip chip-two"><Icon name="users" size={15} /> Entrega en persona</span>
        </div>
        <div className="login-visual-footer"><span>GESTIÓN INTELIGENTE</span><span>© GIRA 2026</span></div>
      </section>
      <section className="login-main">
        {sent ? (
          <div className="login-form-wrap enter">
            <div className="login-mobile-brand"><span className="brand-mark">G</span><span className="brand-name">GIRA<span>.</span></span></div>
            <div className="recover-done">
              <span className="created-success"><Icon name="check" size={20} /></span>
              <span className="eyebrow">SOLICITUD ENVIADA</span>
              <h2>Habla con tu administrador</h2>
              <p>Si tu correo está registrado, notificamos a un administrador. <strong>Contacta a tu administrador</strong> para recibir una nueva contraseña temporal.</p>
              <span className="field-hint">Te pedirá cambiarla la primera vez que entres.</span>
              <div className="recover-actions"><Link className="button button-secondary" to="/login">Volver a iniciar sesión</Link></div>
            </div>
          </div>
        ) : (
          <div className="login-form-wrap enter">
            <div className="login-mobile-brand"><span className="brand-mark">G</span><span className="brand-name">GIRA<span>.</span></span></div>
            <Link to="/login" className="back-link"><Icon name="back" size={16} /> Volver a iniciar sesión</Link>
            <div className="login-heading"><span className="eyebrow">RECUPERAR ACCESO</span><h1>¿Olvidaste tu contraseña?</h1><p>Escribe tu correo y avisaremos a un administrador para que te entregue una contraseña temporal.</p></div>
            <form className="login-form" onSubmit={submit}>
              <label className="field-label" htmlFor="recover-email">Correo electrónico</label>
              <div className="input-wrap"><span className="input-leading">✉</span><input id="recover-email" type="email" autoComplete="username" placeholder="nombre@empresa.com" value={email} onChange={(event) => setEmail(event.target.value)} disabled={loading} required maxLength={254} /></div>
              {error && <div className="form-error" role="alert"><span>!</span>{error}</div>}
              <button className="button button-primary login-submit" type="submit" disabled={loading || cooldown > 0}>{loading ? <><span className="button-spinner" /> Enviando…</> : <>Enviar solicitud <Icon name="arrow" size={16} /></>}</button>
            </form>
            <div className="recover-note"><Icon name="shield" size={14} /> <span>Por seguridad no confirmamos si un correo está registrado. Nadie recibe tu contraseña por mensaje ni por correo.</span></div>
          </div>
        )}
      </section>
    </main>
  );
}

function defaultPath(user) {
  return user?.roles?.includes('Administrador') ? '/seguridad/usuarios' : '/inicio';
}
