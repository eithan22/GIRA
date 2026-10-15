import { Link, Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from './security/AuthContext.jsx';
import AccessDenied from './security/pages/AccessDenied.jsx';
import Login from './security/pages/Login.jsx';
import UserDetail from './security/pages/UserDetail.jsx';
import UserForm from './security/pages/UserForm.jsx';
import Users from './security/pages/Users.jsx';
import AppLayout from './security/components/AppLayout.jsx';
import RequireRole from './security/components/RequireRole.jsx';
import Icon from './security/components/Icon.jsx';

function Home() {
  const { user } = useAuth();
  const isAdmin = user?.roles?.includes('Administrador');
  return (
    <section className="welcome-panel enter">
      <div className="welcome-icon"><Icon name="shield" size={25} /></div>
      <span className="eyebrow">CENTRO DE SEGURIDAD</span>
      <h1>Hola, {user?.nombre?.split(' ')[0] || 'bienvenido'}.</h1>
      <p>Tu espacio de trabajo está listo. Desde aquí puedes revisar tu perfil y los accesos disponibles para tu rol.</p>
      <div className="welcome-actions">
        {isAdmin && <Link className="button button-primary" to="/seguridad/usuarios">Gestionar usuarios <Icon name="arrow" size={16} /></Link>}
      </div>
      <div className="welcome-footer"><span className="status-dot" /> Sesión segura activa <span className="welcome-role">{user?.roles?.join(' · ')}</span></div>
    </section>
  );
}

function AuthenticatedApp() {
  const { user } = useAuth();
  const home = user?.roles?.includes('Administrador') ? '/seguridad/usuarios' : '/inicio';
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route element={<RequireRole />}>
        <Route element={<AppLayout />}>
          <Route index element={<Navigate to={home} replace />} />
          <Route path="inicio" element={<Home />} />
          <Route path="acceso-denegado" element={<AccessDenied />} />
          <Route element={<RequireRole roles={['Administrador']} />}>
            <Route path="seguridad/usuarios" element={<Users />} />
            <Route path="seguridad/usuarios/nuevo" element={<UserForm />} />
            <Route path="seguridad/usuarios/:userId/editar" element={<UserForm />} />
            <Route path="seguridad/usuarios/:userId" element={<UserDetail />} />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Route>
    </Routes>
  );
}

export default function App() {
  const { loading } = useAuth();
  if (loading) return <div className="boot-screen"><span className="brand-mark">G</span><span className="loading-orb" /> Cargando tu espacio seguro…</div>;
  return <AuthenticatedApp />;
}
