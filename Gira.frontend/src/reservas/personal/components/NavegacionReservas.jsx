import { NavLink } from 'react-router-dom';
import { useAuth } from '../../../security/AuthContext.jsx';
import {
  ETIQUETA_NAVEGACION_RESERVAS,
  TEXTO_PESTANA_AGENDA,
  TEXTO_PESTANA_CONFIGURACION,
  TEXTO_PESTANA_MAPA,
} from '../reservasPersonalTextos.js';

const ROLES_CONFIGURACION = ['Administrador', 'Gerente'];

/**
 * Navegación entre las pantallas del personal: un <nav> con enlaces reales; NavLink marca la activa
 * con aria-current="page". «Configuración» solo se muestra a Administrador y Gerente por
 * comodidad: la protección real es la ruta (RequireRole en App.jsx), no este enlace.
 */
export default function NavegacionReservas() {
  const { user } = useAuth();
  const veConfiguracion = ROLES_CONFIGURACION.some((rol) => user?.roles?.includes(rol));

  const enlaces = [
    ['/reservas/agenda', TEXTO_PESTANA_AGENDA],
    ['/reservas/mapa', TEXTO_PESTANA_MAPA],
    ...(veConfiguracion ? [['/reservas/configuracion', TEXTO_PESTANA_CONFIGURACION]] : []),
  ];

  return (
    <nav className="reservas-personal-pestanas" aria-label={ETIQUETA_NAVEGACION_RESERVAS}>
      {enlaces.map(([ruta, etiqueta]) => (
        <NavLink
          key={ruta}
          to={ruta}
          end
          className={({ isActive }) => `reservas-personal-pestana${isActive ? ' reservas-personal-pestana--activa' : ''}`}
        >
          {etiqueta}
        </NavLink>
      ))}
    </nav>
  );
}
