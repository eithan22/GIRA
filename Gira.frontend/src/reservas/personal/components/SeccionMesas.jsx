import { mesaConZonaInactiva } from '../reservasPersonalFormato.js';
import { TEXTO_PESTANA_MESAS, TEXTO_ZONA_INACTIVA_ETIQUETA } from '../reservasPersonalTextos.js';
import EstadoActiva from './EstadoActiva.jsx';
import PanelMesa from './PanelMesa.jsx';
import SeccionSalon from './SeccionSalon.jsx';

/**
 * Pestaña «Mesas»: lista filtrable por zona y estado. Una mesa activa cuya zona está inactiva lleva
 * además la etiqueta «Zona inactiva» (con texto): no se ofrece mientras la zona siga inactiva.
 */
export default function SeccionMesas({ zonas }) {
  const columnas = [
    { clave: 'numero', titulo: 'Número', render: (mesa) => `Mesa ${mesa.numero}` },
    { clave: 'zona', titulo: 'Zona', render: (mesa) => mesa.zonaNombre },
    { clave: 'capacidad', titulo: 'Capacidad', render: (mesa) => mesa.capacidad },
    {
      clave: 'estado',
      titulo: 'Estado',
      render: (mesa) => (
        <EstadoActiva
          activa={mesa.activa}
          extra={mesaConZonaInactiva(mesa, zonas.zonas) ? TEXTO_ZONA_INACTIVA_ETIQUETA : undefined}
        />
      ),
    },
  ];

  return (
    <SeccionSalon
      recurso="mesas"
      titulo={TEXTO_PESTANA_MESAS}
      columnas={columnas}
      Formulario={PanelMesa}
      nombreDe={(mesa) => String(mesa.numero)}
      zonas={zonas}
      filtroZona
    />
  );
}
