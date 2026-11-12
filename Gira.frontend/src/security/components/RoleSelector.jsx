import { useEffect, useState } from 'react';
import { authService } from '../authService.js';
import Icon from './Icon.jsx';

export default function RoleSelector({ selected = [], onChange }) {
  const [roles, setRoles] = useState([]);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    authService.getRoles().then((data) => {
      if (active) setRoles(Array.isArray(data) ? data : data.items ?? data.roles ?? []);
    }).catch(() => setError('No se pudieron cargar los roles. Intenta de nuevo.'));
    return () => { active = false; };
  }, []);

  function toggle(role) {
    onChange(selected.includes(role) ? selected.filter((item) => item !== role) : [...selected, role]);
  }

  return (
    <div className="role-selector">
      {error && <div className="inline-error">{error}</div>}
      {roles.map((role) => {
        const name = typeof role === 'string' ? role : role.name ?? role.nombre;
        const description = typeof role === 'string' ? '' : role.description ?? role.descripcion ?? '';
        const checked = selected.includes(name);
        return (
          <button type="button" className={`role-option${checked ? ' selected' : ''}`} key={name} onClick={() => toggle(name)} aria-pressed={checked}>
            <span className={`role-check${checked ? ' checked' : ''}`}>{checked && <Icon name="check" size={13} />}</span>
            <span className="role-copy"><strong>{name}</strong><small>{description || roleDescription(name)}</small></span>
          </button>
        );
      })}
      {!roles.length && !error && <span className="field-hint">Cargando roles…</span>}
    </div>
  );
}

function roleDescription(role) {
  return {
    Administrador: 'Gestiona usuarios y configuración del sistema.',
    Gerente: 'Accede a información de gestión y supervisión.',
    Supervisor: 'Supervisa las operaciones y el equipo.',
    Mesero: 'Accede a las herramientas de servicio de sala.',
    Cocina: 'Accede a las herramientas de cocina.',
  }[role] || 'Permisos asignados a este rol.';
}
