import { formatearHora } from '../../reservasFormato.js';
import { TEXTO_PESTANA_FRANJAS } from '../reservasPersonalTextos.js';
import EstadoActiva from './EstadoActiva.jsx';
import PanelFranja from './PanelFranja.jsx';
import SeccionSalon from './SeccionSalon.jsx';

const COLUMNAS = [
  { clave: 'nombre', titulo: 'Nombre', render: (franja) => franja.nombre },
  { clave: 'horario', titulo: 'Horario', render: (franja) => `${formatearHora(franja.horaInicio)}–${formatearHora(franja.horaFin)}` },
  { clave: 'estado', titulo: 'Estado', render: (franja) => <EstadoActiva activa={franja.activa} /> },
];

/** Pestaña «Franjas»: lista ordenada por hora de inicio, crear, editar y activar o desactivar. */
export default function SeccionFranjas() {
  return (
    <SeccionSalon
      recurso="franjas"
      titulo={TEXTO_PESTANA_FRANJAS}
      columnas={COLUMNAS}
      Formulario={PanelFranja}
      nombreDe={(franja) => franja.nombre}
    />
  );
}
