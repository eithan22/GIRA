import { useEffect, useState } from 'react';
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../AuthContext.jsx';
import Icon from '../components/Icon.jsx';

export default function Login() {
  const { user, login, sessionError, clearUser } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const signedOut = Boolean(location.state?.signedOut);

  useEffect(() => {
    if (signedOut) clearUser();
  }, [signedOut, clearUser]);

  if (user && !signedOut) return <Navigate to={defaultPath(user)} replace />;
  const returnUrl = location.state?.from?.pathname
    ? `${location.state.from.pathname}${location.state.from.search || ''}${location.state.from.hash || ''}`
    : searchParams.get('returnUrl');
  const redirectTo = (authenticatedUser) => {
    const target = isSafeInternalPath(returnUrl) ? returnUrl : defaultPath(authenticatedUser);
    navigate(target || defaultPath(authenticatedUser), { replace: true });
  };

  async function submit(event) {
    event.preventDefault();
    setError('');
    setLoading(true);
    try {
      const authenticatedUser = await login({ email: email.trim(), password });
      redirectTo(authenticatedUser);
    } catch (requestError) {
      const data = requestError.response?.data;
      setError(data?.detail || data?.message || data?.title || 'No pudimos iniciar sesión. Revisa tu correo y contraseña.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="login-screen">
      <section className="login-visual">
        <a href="/" className="brand login-brand"><span className="brand-mark">G</span><span className="brand-name">GIRA<span>.</span></span></a>
        <div className="login-art">
          <div className="orbit orbit-one" /><div className="orbit orbit-two" />
          <div className="login-art-card"><div className="art-lock"><Icon name="shield" size={31} /></div><span className="eyebrow">PROTECCIÓN INTELIGENTE</span><h2>El control empieza<br />con un acceso seguro.</h2><p>Un espacio de trabajo diseñado para tu equipo y protegido por tus permisos.</p><div className="art-lines"><span /><span /><span /></div></div>
          <span className="floating-chip chip-one"><span className="status-dot" /> Acceso verificado</span><span className="floating-chip chip-two"><Icon name="key" size={15} /> Control por roles</span>
        </div>
        <div className="login-visual-footer"><span>GESTIÓN INTELIGENTE</span><span>© GIRA 2026</span></div>
      </section>
      <section className="login-main">
        <div className="login-form-wrap enter">
          <div className="login-mobile-brand"><span className="brand-mark">G</span><span className="brand-name">GIRA<span>.</span></span></div>
          <div className="login-heading"><span className="eyebrow">BIENVENIDO DE NUEVO</span><h1>Inicia sesión</h1><p>Ingresa tus credenciales para acceder a tu espacio.</p></div>
          {location.state?.notice && <div className="form-error" role="status"><span>!</span>{location.state.notice}</div>}
          <form className="login-form" onSubmit={submit}>
            <label className="field-label" htmlFor="email">Correo electrónico</label>
            <div className="input-wrap"><span className="input-leading">✉</span><input id="email" type="email" autoComplete="username" placeholder="nombre@empresa.com" value={email} onChange={(event) => setEmail(event.target.value)} required /></div>
            <div className="password-label-row"><label className="field-label" htmlFor="password">Contraseña</label><span className="field-hint">Acceso protegido</span></div>
            <div className="input-wrap"><Icon className="input-leading-icon" name="lock" size={16} /><input id="password" type={showPassword ? 'text' : 'password'} autoComplete="current-password" placeholder="Ingresa tu contraseña" value={password} onChange={(event) => setPassword(event.target.value)} required minLength={1} /><button className="input-action" type="button" onClick={() => setShowPassword(!showPassword)} aria-label={showPassword ? 'Ocultar contraseña' : 'Mostrar contraseña'}><Icon name="eye" size={17} /></button></div>
            {(error || sessionError) && <div className="form-error" role="alert"><span>!</span>{error || sessionError}</div>}
            <button className="button button-primary login-submit" type="submit" disabled={loading}>{loading ? <><span className="button-spinner" /> Verificando…</> : <>Entrar a GIRA <Icon name="arrow" size={16} /></>}</button>
          </form>
          <p className="login-security"><Icon name="shield" size={15} /> Tu sesión está protegida y tus accesos son monitoreados.</p>
          <Link to="/login" className="sr-only">Inicio de sesión</Link>
        </div>
      </section>
    </main>
  );
}

function defaultPath(user) {
  return user?.roles?.includes('Administrador') ? '/seguridad/usuarios' : '/inicio';
}

function isSafeInternalPath(path) {
  if (typeof path !== 'string' || !path.startsWith('/') || path.startsWith('//') || path.includes('\\')) return false;
  try {
    return new URL(path, window.location.origin).origin === window.location.origin;
  } catch {
    return false;
  }
}
