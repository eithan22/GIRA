import { useEffect, useRef, useState } from 'react';
import Aviso from '../../components/Aviso.jsx';
import { contarMesasZona, textoImpactoZona } from '../reservasPersonalFormato.js';
import { esPeticionCancelada, mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';
import {
  CONSECUENCIA_DESACTIVAR,
  CONSECUENCIA_REACTIVAR,
  RECURSOS_SALON,
  TEXTO_CARGANDO_IMPACTO,
  TEXTO_CONFIRMAR_DESACTIVAR,
  TEXTO_CONFIRMAR_REACTIVAR,
  TEXTO_IMPACTO_NO_CARGO,
  TEXTO_PROCESANDO,
  TEXTO_VOLVER,
  tituloCambioEstado,
} from '../reservasPersonalTextos.js';
import ErrorSalon from './ErrorSalon.jsx';
import PanelPersonal from './PanelPersonal.jsx';

const CAMBIAR = {
  zonas: (id, activa) => reservasPersonalService.cambiarEstadoZona(id, activa),
  mesas: (id, activa) => reservasPersonalService.cambiarEstadoMesa(id, activa),
  franjas: (id, activa) => reservasPersonalService.cambiarEstadoFranja(id, activa),
};

/**
 * Confirmación explícita de desactivar o reactivar (PATCH /api/{recurso}/{id}/estado). Siempre explica
 * la consecuencia con lo que el backend hace. Al desactivar una zona consulta sus mesas
 * (GET /api/mesas?zonaId=) para mostrar cuántas tiene; de reservas futuras el backend no da un conteo
 * por adelantado: solo llega en el texto del 409, que se muestra tal cual y deja el panel abierto.
 * Reactivar es una confirmación breve, que también explica su consecuencia.
 */
export default function PanelCambioEstado({
  recurso, item, activar, nombre, origenRef, fallbackFocoRef, onCerrar, onCambiada,
}) {
  const { singular } = RECURSOS_SALON[recurso];
  const consultaImpacto = recurso === 'zonas' && !activar;
  const [impacto, setImpacto] = useState({ estado: consultaImpacto ? 'cargando' : 'idle', datos: null });
  const [error, setError] = useState('');
  const [enviando, setEnviando] = useState(false);
  const envioEnCursoRef = useRef(false);
  const volverRef = useRef(null);

  useEffect(() => {
    volverRef.current?.focus();
  }, []);

  useEffect(() => {
    if (!consultaImpacto) return undefined;
    const controlador = new AbortController();
    reservasPersonalService.listarMesas({ zonaId: item.id, page: 1, pageSize: 100 }, { signal: controlador.signal })
      .then((pagina) => setImpacto({ estado: 'listo', datos: contarMesasZona(pagina) }))
      .catch((fallo) => {
        if (!esPeticionCancelada(fallo)) setImpacto({ estado: 'error', datos: null });
      });
    return () => controlador.abort();
  }, [consultaImpacto, item.id]);

  async function confirmar() {
    if (envioEnCursoRef.current) return;
    envioEnCursoRef.current = true;
    setEnviando(true);
    setError('');
    try {
      const actualizado = await CAMBIAR[recurso](item.id, activar);
      onCambiada(actualizado);
    } catch (fallo) {
      setError(mensajeDeError(fallo));
    } finally {
      envioEnCursoRef.current = false;
      setEnviando(false);
    }
  }

  const consecuencia = activar ? CONSECUENCIA_REACTIVAR[singular] : CONSECUENCIA_DESACTIVAR[singular];

  return (
    <PanelPersonal
      titulo={tituloCambioEstado(activar, singular, nombre)}
      tituloId="panel-estado-titulo"
      eyebrow={recurso.toUpperCase()}
      onCerrar={onCerrar}
      bloqueado={enviando}
      origenRef={origenRef}
      fallbackFocoRef={fallbackFocoRef}
    >
      <section
        className={`reservas-personal-confirmacion${activar ? '' : ' reservas-personal-confirmacion--peligro'}`}
        role="alertdialog"
        aria-labelledby="panel-estado-titulo"
        aria-describedby="estado-consecuencia"
      >
        <p id="estado-consecuencia" className="reservas-personal-confirmacion-texto">{consecuencia}</p>

        {consultaImpacto && (
          <div role="status" aria-live="polite">
            {impacto.estado === 'cargando' && <p className="reservas-campo-ayuda">{TEXTO_CARGANDO_IMPACTO}</p>}
            {impacto.estado === 'listo' && <p className="reservas-personal-confirmacion-texto"><strong>{textoImpactoZona(impacto.datos)}</strong></p>}
            {impacto.estado === 'error' && <Aviso tipo="advertencia">{TEXTO_IMPACTO_NO_CARGO}</Aviso>}
          </div>
        )}

        {error && <ErrorSalon texto={error} />}

        <div className="reservas-personal-confirmacion-botones">
          <button
            type="button"
            className={`reservas-boton reservas-boton--chico ${activar ? 'reservas-boton--primario' : 'reservas-boton--peligro'} reservas-personal-boton-auto`}
            onClick={confirmar}
            disabled={enviando}
          >{enviando ? TEXTO_PROCESANDO : (activar ? TEXTO_CONFIRMAR_REACTIVAR : TEXTO_CONFIRMAR_DESACTIVAR)}</button>
          <button
            type="button"
            ref={volverRef}
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={onCerrar}
            disabled={enviando}
          >{TEXTO_VOLVER}</button>
        </div>
      </section>
    </PanelPersonal>
  );
}
