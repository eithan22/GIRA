import { useCallback, useRef, useState } from 'react';
import ConfirmarDescartar from '../components/ConfirmarDescartar.jsx';
import PaginaPersonal from '../components/PaginaPersonal.jsx';
import PestanasConfiguracion from '../components/PestanasConfiguracion.jsx';
import SeccionDiasCerrados from '../components/SeccionDiasCerrados.jsx';
import SeccionFranjas from '../components/SeccionFranjas.jsx';
import SeccionHorario from '../components/SeccionHorario.jsx';
import SeccionMesas from '../components/SeccionMesas.jsx';
import SeccionPoliticas from '../components/SeccionPoliticas.jsx';
import SeccionZonas from '../components/SeccionZonas.jsx';
import { useZonasSalon } from '../hooks/useZonasSalon.js';
import {
  CUERPO_CAMBIOS_SIN_GUARDAR,
  TEXTO_DESCARTAR_Y_CAMBIAR,
  TITULO_CAMBIOS_SIN_GUARDAR,
} from '../reservasPersonalTextos.js';

/**
 * Ruta /reservas/configuracion: administración de zonas, mesas y franjas (Lote 5, GI-397) y de las políticas, el
 * horario y los días cerrados de reservas (GI-226, Lote 6). Solo Administrador y Gerente: la protección real es la
 * ruta (RequireRole en App.jsx) y las políticas del backend (GestionarSalon, GestionarConfiguracion); un Mesero o
 * Supervisor que entre por la URL va a «Acceso denegado». La pestaña elegida vive solo en el estado de la página.
 * Las pestañas de Políticas, Horario y Días cerrados avisan si tienen cambios sin guardar (`onSucio`): al cambiar
 * de pestaña con cambios se pide confirmación. Las zonas (hasta 100) se cargan aquí para el filtro y el formulario
 * de mesas, y se recargan cuando cambia una zona.
 */
export default function ConfiguracionPersonal() {
  const [pestana, setPestana] = useState('zonas');
  const [pendiente, setPendiente] = useState(null); // pestaña a la que se quiere ir, a la espera de confirmar
  const sucioRef = useRef(false);
  const zonas = useZonasSalon();

  function cambiar(clave) {
    if (clave === pestana) return;
    if (sucioRef.current) setPendiente(clave);
    else setPestana(clave);
  }

  function seguirEditando() {
    setPendiente(null);
    document.getElementById(`pestana-${pestana}`)?.focus();
  }

  function descartarYCambiar() {
    sucioRef.current = false;
    setPestana(pendiente);
    setPendiente(null);
    // El foco queda en la pestaña elegida (la anterior pudo desmontarse).
    requestAnimationFrame(() => document.getElementById(`pestana-${pendiente}`)?.focus());
  }

  // Estable: las pestañas lo usan como dependencia de efectos.
  const marcarSucio = useCallback((sucio) => { sucioRef.current = sucio; }, []);

  return (
    <PaginaPersonal clave="configuracion">
      <PestanasConfiguracion activa={pestana} onCambiar={cambiar} />
      {pendiente && (
        <ConfirmarDescartar
          onSeguir={seguirEditando}
          onDescartar={descartarYCambiar}
          titulo={TITULO_CAMBIOS_SIN_GUARDAR}
          cuerpo={CUERPO_CAMBIOS_SIN_GUARDAR}
          textoDescartar={TEXTO_DESCARTAR_Y_CAMBIAR}
        />
      )}
      <div role="tabpanel" id={`panel-${pestana}`} aria-labelledby={`pestana-${pestana}`}>
        {pestana === 'zonas' && <SeccionZonas onCambio={zonas.recargar} />}
        {pestana === 'mesas' && <SeccionMesas zonas={zonas} />}
        {pestana === 'franjas' && <SeccionFranjas />}
        {pestana === 'politicas' && <SeccionPoliticas onSucio={marcarSucio} />}
        {pestana === 'horario' && <SeccionHorario onSucio={marcarSucio} />}
        {pestana === 'dias-cerrados' && <SeccionDiasCerrados onSucio={marcarSucio} />}
      </div>
    </PaginaPersonal>
  );
}
