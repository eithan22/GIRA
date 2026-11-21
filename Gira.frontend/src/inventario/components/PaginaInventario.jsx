import { useEffect } from 'react';
import { NavLink } from 'react-router-dom';
import { useAuth } from '../../security/AuthContext.jsx';
import Icon from '../../security/components/Icon.jsx';
import { ROLES_ATENDER, ROLES_CONSULTAR, ROLES_GESTIONAR, tieneRol } from '../inventarioFormato.js';
import '../inventario.css';

// Las pestañas se ocultan según el rol solo por comodidad: la protección real son las rutas y el backend.
const PESTANAS = [
  { to: '/inventario/insumos', texto: 'Insumos', roles: ROLES_CONSULTAR },
  { to: '/inventario/platos', texto: 'Platos y recetas', roles: ROLES_CONSULTAR },
  { to: '/inventario/movimientos', texto: 'Movimientos', roles: ROLES_CONSULTAR },
  { to: '/inventario/kardex', texto: 'Kardex', roles: ROLES_ATENDER },
  { to: '/inventario/alertas', texto: 'Alertas', roles: ROLES_ATENDER },
  { to: '/inventario/proyeccion', texto: 'Proyección', roles: ROLES_ATENDER },
  { to: '/inventario/catalogos', texto: 'Catálogos', roles: ROLES_GESTIONAR },
];

/** Esqueleto de las pantallas de Inventario: título, pestañas y contenido. */
export default function PaginaInventario({ titulo, descripcion, accion, children }) {
  const { user } = useAuth();

  useEffect(() => { document.title = `${titulo} · GIRA`; }, [titulo]);

  return (
    <section className="page-section enter inventario">
      <div className="page-heading-row">
        <div><span className="eyebrow">INVENTARIO INTELIGENTE</span><h1>{titulo}</h1><p>{descripcion}</p></div>
        {accion}
      </div>
      <nav className="inv-tabs" aria-label="Secciones de inventario">
        {PESTANAS.filter((p) => tieneRol(user, p.roles)).map((p) => (
          <NavLink key={p.to} to={p.to} className={({ isActive }) => `inv-tab${isActive ? ' active' : ''}`}>{p.texto}</NavLink>
        ))}
      </nav>
      {children}
    </section>
  );
}

/** Aviso de error que se puede cerrar o, si falló una carga, reintentar. */
export function AvisoError({ mensaje, onCerrar, onReintentar }) {
  if (!mensaje) return null;
  return (
    <div className="notice-error" role="alert">
      <span>!</span>{mensaje}
      {onReintentar && <button type="button" className="button button-secondary button-small inv-reintentar" onClick={onReintentar}>Reintentar</button>}
      {onCerrar && <button type="button" className="notice-dismiss" onClick={onCerrar} aria-label="Cerrar aviso"><Icon name="close" size={16} /></button>}
    </div>
  );
}

/** Pie de tabla con «Anterior / Siguiente». */
export function Paginacion({ page, pageSize, total, cargando, onCambiar }) {
  const paginas = Math.max(1, Math.ceil(total / pageSize));
  return (
    <div className="pagination">
      <span>Mostrando <strong>{total ? (page - 1) * pageSize + 1 : 0}–{Math.min(page * pageSize, total)}</strong> de <strong>{total}</strong></span>
      <div className="pagination-actions">
        <button type="button" className="button button-secondary button-small" disabled={page <= 1 || cargando} onClick={() => onCambiar(page - 1)}>Anterior</button>
        <span className="page-number">{page} / {paginas}</span>
        <button type="button" className="button button-secondary button-small" disabled={page >= paginas || cargando} onClick={() => onCambiar(page + 1)}>Siguiente</button>
      </div>
    </div>
  );
}

/** Fila de tabla para «cargando» o «vacío». */
export function FilaEstado({ columnas, cargando, vacio }) {
  return (
    <tr>
      <td colSpan={columnas} className={cargando ? 'table-message' : undefined}>
        {cargando
          ? <><span className="button-spinner" /> Cargando…</>
          : <div className="empty-state"><span className="empty-icon"><Icon name="search" size={21} /></span><strong>{vacio}</strong></div>}
      </td>
    </tr>
  );
}
