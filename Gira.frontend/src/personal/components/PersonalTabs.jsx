import { NavLink } from 'react-router-dom';

// Navegación entre las pantallas de Personal y Turnos (cargos, empleados, requerimientos y ausencias).
const PESTANAS = [
  { to: '/personal/empleados', etiqueta: 'Empleados' },
  { to: '/personal/cargos', etiqueta: 'Cargos' },
  { to: '/personal/requerimientos', etiqueta: 'Requerimientos' },
  { to: '/personal/ausencias', etiqueta: 'Ausencias' },
];

export default function PersonalTabs() {
  return (
    <nav className="personal-tabs" aria-label="Secciones de Personal">
      {PESTANAS.map((pestana) => (
        <NavLink
          key={pestana.to}
          to={pestana.to}
          className={({ isActive }) => `button button-small ${isActive ? 'button-primary' : 'button-secondary'}`}
        >
          {pestana.etiqueta}
        </NavLink>
      ))}
    </nav>
  );
}
