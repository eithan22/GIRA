import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../AuthContext.jsx';
import Icon from '../components/Icon.jsx';

export default function AccessDenied() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const home = user?.roles?.includes('Administrador') ? '/seguridad/usuarios' : '/inicio';
  return (
    <section className="denied-screen enter">
      <div className="denied-illustration"><span className="denied-orbit" /><span className="denied-shield"><Icon name="lock" size={31} /></span><span className="denied-code">403</span></div>
      <span className="eyebrow">ACCESO RESTRINGIDO</span><h1>No tienes permiso para entrar aquí.</h1>
      <p>Tu sesión sigue activa, pero este espacio está disponible para otros roles. Si crees que es un error, contacta al administrador.</p>
      <div className="denied-actions"><button type="button" className="button button-primary" onClick={() => navigate(home, { replace: true })}>Volver a mi inicio <Icon name="arrow" size={16} /></button><Link to="/perfil/cambiar-password" className="text-link">Ir a mi perfil</Link></div>
    </section>
  );
}
