import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../AuthContext.jsx';
import Icon from './Icon.jsx';

const menuItems = [
  { label: 'Inicio', to: '/inicio', icon: 'grid', roles: ['Administrador', 'Gerente', 'Supervisor', 'Mesero', 'Cocina'] },
  { label: 'Usuarios', to: '/seguridad/usuarios', icon: 'users', roles: ['Administrador'] },
];

export default function AppLayout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const allowedItems = menuItems.filter((item) => item.roles.some((role) => user?.roles?.includes(role)));

  async function handleLogout() {
    try {
      await logout();
      navigate('/login', {
        replace: true,
        state: { signedOut: true },
      });
    } catch {
      navigate('/login', {
        replace: true,
        state: {
          signedOut: true,
          notice: 'La sesión se cerró en este dispositivo, pero no se pudo confirmar el cierre en el servidor.',
        },
      });
    }
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <Link to="/inicio" className="brand"><span className="brand-mark">G</span><span className="brand-name">GIRA<span>.</span></span></Link>
        <div className="workspace-label">ESPACIO DE TRABAJO</div>
        <div className="restaurant-switcher"><span className="restaurant-avatar">S</span><span className="restaurant-name">Seguridad<small>Acceso y usuarios</small></span><Icon name="chevron" size={15} /></div>
        <nav className="side-nav" aria-label="Menú principal">
          <div className="nav-heading">MENÚ PRINCIPAL</div>
          {allowedItems.map((item) => (
            <NavLink to={item.to} key={item.to} className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`}>
              <Icon name={item.icon} size={17} /><span>{item.label}</span>
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-bottom">
          <div className="security-note"><div className="security-note-icon"><Icon name="shield" size={17} /></div><strong>Tu cuenta protegida</strong><p>El acceso a GIRA está protegido según tus permisos.</p></div>
          <div className="profile-button">
            <span className="profile-avatar">{user?.nombre?.charAt(0) || 'U'}</span><span className="profile-copy">{user?.nombre}<small>{user?.roles?.join(' · ')}</small></span><Icon name="more" size={18} />
          </div>
          <button type="button" className="sidebar-logout" onClick={handleLogout}><Icon name="logout" size={16} /> Cerrar sesión</button>
        </div>
      </aside>
      <main className="main-area">
        <header className="topbar">
          <div className="breadcrumbs"><span>GIRA</span><Icon name="chevron" size={14} /><strong>{locationLabel()}</strong></div>
          <div className="topbar-right"><span className="topbar-secure"><span className="status-dot" /> Sesión segura</span><span className="topbar-avatar">{user?.nombre?.charAt(0)}</span></div>
        </header>
        <div className="page-content"><Outlet /></div>
      </main>
    </div>
  );
}

function locationLabel() {
  const path = window.location.pathname;
  if (path.includes('/perfil/cambiar-password')) return 'Cambiar contraseña';
  if (path.includes('/usuarios')) return path.includes('nuevo') ? 'Nuevo usuario' : path.includes('editar') ? 'Editar usuario' : path.split('/').length > 3 ? 'Detalle de usuario' : 'Usuarios';
  if (path.includes('acceso-denegado')) return 'Acceso denegado';
  return 'Inicio';
}
