import { useEffect, useState } from 'react';
import { POLITICAS_POR_DEFECTO } from '../../reservasFormato.js';
import { reservasPersonalService } from '../reservasPersonalService.js';

const VIGENCIA_MS = 60_000;

// Una sola petición compartida y una vigencia corta, como usePoliticasReserva en la parte pública.
let guardados = null; // { valores, momento }
let enCurso = null;

function aEnteros(dto) {
  const p = dto?.parametros ?? {};
  const entero = (valor, porDefecto, minimo) => (Number.isInteger(valor) && valor >= minimo ? valor : porDefecto);
  return {
    antelacionMinimaMinutos: entero(p.antelacionMinimaMinutos, POLITICAS_POR_DEFECTO.antelacionMinimaMinutos, 0),
    antelacionMaximaDias: entero(p.antelacionMaximaDias, POLITICAS_POR_DEFECTO.antelacionMaximaDias, 1),
  };
}

function cargar() {
  if (guardados && Date.now() - guardados.momento < VIGENCIA_MS) return Promise.resolve(guardados.valores);
  if (!enCurso) {
    enCurso = reservasPersonalService.obtenerParametrosReserva()
      .then((dto) => {
        const valores = aEnteros(dto);
        guardados = { valores, momento: Date.now() };
        return valores;
      })
      .catch(() => null) // si falla se siguen usando los valores por defecto: nunca bloquea el formulario
      .finally(() => { enCurso = null; });
  }
  return enCurso;
}

/** Descarta lo guardado: lo llama la pantalla de Configuración después de guardar las políticas. */
export function invalidarParametrosOperativos() {
  guardados = null;
}

/**
 * Antelación mínima (minutos) y máxima (días) que usa el personal en Agenda (GI-403): salen de
 * GET /api/configuracion/reservas/parametros (OperarReservas). Mientras cargan, o si falla la petición,
 * valen POLITICAS_POR_DEFECTO. Informativo: el servidor decide cada reserva.
 */
export function useParametrosOperativos() {
  const [valores, setValores] = useState(() => guardados?.valores ?? {
    antelacionMinimaMinutos: POLITICAS_POR_DEFECTO.antelacionMinimaMinutos,
    antelacionMaximaDias: POLITICAS_POR_DEFECTO.antelacionMaximaDias,
  });

  useEffect(() => {
    let activo = true;
    cargar().then((cargados) => {
      if (activo && cargados) setValores(cargados);
    });
    return () => { activo = false; };
  }, []);

  return valores;
}
