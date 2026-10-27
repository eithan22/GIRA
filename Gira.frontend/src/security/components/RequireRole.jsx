import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../AuthContext.jsx';

export default function RequireRole({ roles, children }) {
  const { user, loading } = useAuth();
  const location = useLocation();
  if (loading) return <div className="boot-screen"><span className="loading-orb" /> Verificando permisos…</div>;
  if (!user) return <Navigate to="/login" replace state={{ from: location }} />;

  const passwordRoute = location.pathname === '/perfil/cambiar-password';
  if (user.debeCambiarPassword && !passwordRoute) {
    return <Navigate to="/perfil/cambiar-password" replace state={{ forced: true, from: location }} />;
  }
  if (roles?.length && !roles.some((role) => user.roles?.includes(role))) {
    return <Navigate to="/acceso-denegado" replace state={{ from: location }} />;
  }
  return children ?? <Outlet />;
}
