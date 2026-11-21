import { TEXTO_PESTANA_ZONAS } from '../reservasPersonalTextos.js';
import EstadoActiva from './EstadoActiva.jsx';
import PanelZona from './PanelZona.jsx';
import SeccionSalon from './SeccionSalon.jsx';

const COLUMNAS = [
  { clave: 'nombre', titulo: 'Nombre', render: (zona) => zona.nombre },
  { clave: 'estado', titulo: 'Estado', render: (zona) => <EstadoActiva activa={zona.activa} /> },
];

/** Pestaña «Zonas»: lista, crear, editar y activar o desactivar. */
export default function SeccionZonas({ onCambio }) {
  return (
    <SeccionSalon
      recurso="zonas"
      titulo={TEXTO_PESTANA_ZONAS}
      columnas={COLUMNAS}
      Formulario={PanelZona}
      nombreDe={(zona) => zona.nombre}
      onCambio={onCambio}
    />
  );
}
