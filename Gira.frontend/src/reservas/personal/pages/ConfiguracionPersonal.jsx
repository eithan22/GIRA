import { useState } from 'react';
import PaginaPersonal from '../components/PaginaPersonal.jsx';
import PestanasConfiguracion from '../components/PestanasConfiguracion.jsx';
import SeccionFranjas from '../components/SeccionFranjas.jsx';
import SeccionMesas from '../components/SeccionMesas.jsx';
import SeccionZonas from '../components/SeccionZonas.jsx';
import { useZonasSalon } from '../hooks/useZonasSalon.js';

/**
 * Ruta /reservas/configuracion (Lote 5, GI-397): administración de zonas, mesas y franjas. Solo
 * Administrador y Gerente: la protección real es la ruta (RequireRole en App.jsx) y la política
 * GestionarSalon del backend; un Mesero o Supervisor que entre por la URL va a «Acceso denegado».
 * La pestaña elegida vive solo en el estado de la página. Las zonas (hasta 100) se cargan aquí para
 * el filtro y el formulario de mesas, y se recargan cuando cambia una zona.
 */
export default function ConfiguracionPersonal() {
  const [pestana, setPestana] = useState('zonas');
  const zonas = useZonasSalon();

  return (
    <PaginaPersonal clave="configuracion">
      <PestanasConfiguracion activa={pestana} onCambiar={setPestana} />
      <div role="tabpanel" id={`panel-${pestana}`} aria-labelledby={`pestana-${pestana}`}>
        {pestana === 'zonas' && <SeccionZonas onCambio={zonas.recargar} />}
        {pestana === 'mesas' && <SeccionMesas zonas={zonas} />}
        {pestana === 'franjas' && <SeccionFranjas />}
      </div>
    </PaginaPersonal>
  );
}
