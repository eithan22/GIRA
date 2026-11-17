import { useEffect, useRef, useState } from 'react';
import Aviso from '../../components/Aviso.jsx';
import { formatearFechaCorta } from '../../reservasFormato.js';
import { errorPorCampo } from '../../reservasErrores.js';
import { useCargaConfiguracion } from '../hooks/useCargaConfiguracion.js';
import { invalidarParametrosOperativos } from '../hooks/useParametrosOperativos.js';
import {
  MAXIMO_MOTIVO_DIA_CERRADO,
  armarCuerpoDiaCerrado,
  hoyEnSantoDomingo,
  validarDiaCerrado,
} from '../reservasPersonalFormato.js';
import { mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';
import {
  ETIQUETA_FECHA_CIERRE,
  ETIQUETA_MOTIVO_CIERRE,
  EXITO_CIERRE_QUITADO,
  EXITO_DIA_CERRADO,
  INTRO_DIAS_CERRADOS,
  TEXTO_CANCELAR,
  TEXTO_CARGANDO_CONFIGURACION,
  TEXTO_CERRAR_DIA,
  TEXTO_CONFIGURACION_NO_CARGO,
  TEXTO_CONFIRMAR_QUITAR,
  TEXTO_PROCESANDO,
  TEXTO_QUITAR_CIERRE,
  TEXTO_SIN_DIAS_CERRADOS,
  textoPreguntaQuitar,
} from '../reservasPersonalTextos.js';
import EstadoPagina from './EstadoPagina.jsx';

/**
 * Pestaña «Días cerrados» (GI-226, RF-CFG-03): fechas concretas cerradas, de hoy en adelante. Agregar (con motivo
 * opcional) avisa cuántas reservas vigentes ya hay ese día (no se cancelan); una fecha ya cerrada es un 409 del
 * servidor, mostrado tal cual. Quitar pide confirmación.
 */
export default function SeccionDiasCerrados({ onSucio }) {
  const carga = useCargaConfiguracion((opciones) => reservasPersonalService.listarDiasCerrados(opciones));

  if (carga.estado === 'cargando') return <EstadoPagina estado="cargando" mensaje={TEXTO_CARGANDO_CONFIGURACION} />;
  if (carga.estado === 'error') {
    return <EstadoPagina estado="error" mensaje={carga.error || TEXTO_CONFIGURACION_NO_CARGO} onReintentar={carga.recargar} />;
  }
  return <GestionDiasCerrados inicial={carga.datos ?? []} onSucio={onSucio} />;
}

function GestionDiasCerrados({ inicial, onSucio }) {
  const [hoy] = useState(() => hoyEnSantoDomingo());
  const [dias, setDias] = useState(inicial);
  const [fecha, setFecha] = useState('');
  const [motivo, setMotivo] = useState('');
  const [intentado, setIntentado] = useState(false);
  const [erroresServidor, setErroresServidor] = useState({});
  const [error, setError] = useState('');
  const [resultado, setResultado] = useState(null); // { texto, advertencias }
  const [enviando, setEnviando] = useState(false);
  const [quitando, setQuitando] = useState(null); // id del día cuya confirmación está abierta
  const envioEnCursoRef = useRef(false);
  const fechaRef = useRef(null);
  const motivoRef = useRef(null);
  const avisoRef = useRef(null);

  const sucio = fecha !== '' || motivo !== '';
  const errores = { ...(intentado ? validarDiaCerrado({ fecha, motivo }, hoy) : {}), ...erroresServidor };

  useEffect(() => {
    onSucio(sucio);
  }, [sucio, onSucio]);

  useEffect(() => {
    if (resultado) avisoRef.current?.focus();
  }, [resultado]);

  function ordenar(lista) {
    return [...lista].sort((a, b) => (a.fecha < b.fecha ? -1 : a.fecha > b.fecha ? 1 : 0));
  }

  async function agregar(evento) {
    evento.preventDefault();
    if (envioEnCursoRef.current) return;
    setIntentado(true);
    setError('');
    setResultado(null);
    const invalidos = validarDiaCerrado({ fecha, motivo }, hoy);
    if (Object.keys(invalidos).length > 0) {
      setErroresServidor({});
      (invalidos.fecha ? fechaRef : motivoRef).current?.focus();
      return;
    }

    envioEnCursoRef.current = true;
    setEnviando(true);
    try {
      const creado = await reservasPersonalService.crearDiaCerrado(armarCuerpoDiaCerrado({ fecha, motivo }));
      setDias((actuales) => ordenar([...actuales, creado.diaCerrado]));
      setFecha('');
      setMotivo('');
      setErroresServidor({});
      setIntentado(false);
      setResultado({ texto: EXITO_DIA_CERRADO, advertencias: creado.advertencias ?? [] });
      invalidarParametrosOperativos();
    } catch (fallo) {
      const campos = errorPorCampo(fallo);
      if (Object.keys(campos).length > 0) setErroresServidor(campos);
      else setError(mensajeDeError(fallo)); // incluye el 409 de fecha ya cerrada
    } finally {
      envioEnCursoRef.current = false;
      setEnviando(false);
    }
  }

  async function quitar(dia) {
    if (envioEnCursoRef.current) return;
    envioEnCursoRef.current = true;
    setEnviando(true);
    setError('');
    setResultado(null);
    try {
      await reservasPersonalService.eliminarDiaCerrado(dia.id);
      setDias((actuales) => actuales.filter((d) => d.id !== dia.id));
      setQuitando(null);
      setResultado({ texto: EXITO_CIERRE_QUITADO, advertencias: [] });
      invalidarParametrosOperativos();
    } catch (fallo) {
      setError(mensajeDeError(fallo));
      if (fallo?.response?.status === 404) {
        setDias((actuales) => actuales.filter((d) => d.id !== dia.id)); // ya no existe: la lista se actualiza
        setQuitando(null);
      }
    } finally {
      envioEnCursoRef.current = false;
      setEnviando(false);
    }
  }

  return (
    <div className="reservas-personal-seccion reservas-personal-config">
      <p className="reservas-personal-intro">{INTRO_DIAS_CERRADOS}</p>

      {resultado && (
        <div ref={avisoRef} tabIndex={-1}>
          <Aviso tipo="exito" rol="none">{resultado.texto}</Aviso>
          {resultado.advertencias.map((texto) => <Aviso key={texto} tipo="advertencia">{texto}</Aviso>)}
        </div>
      )}
      {error && <Aviso tipo="error">{error}</Aviso>}

      <form className="reservas-personal-formulario" onSubmit={agregar} noValidate>
        <div className="reservas-personal-fila-campos">
          <div className="reservas-campo">
            <label className="reservas-campo-etiqueta" htmlFor="dia-cerrado-fecha">{ETIQUETA_FECHA_CIERRE} *</label>
            <input
              id="dia-cerrado-fecha"
              ref={fechaRef}
              type="date"
              className="reservas-input"
              min={hoy}
              value={fecha}
              onChange={(evento) => { setFecha(evento.target.value); setErroresServidor({}); }}
              aria-invalid={errores.fecha ? 'true' : undefined}
              aria-describedby={errores.fecha ? 'dia-cerrado-fecha-error' : undefined}
            />
            {errores.fecha && (
              <span id="dia-cerrado-fecha-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{errores.fecha}</span>
            )}
          </div>
          <div className="reservas-campo">
            <label className="reservas-campo-etiqueta" htmlFor="dia-cerrado-motivo">{ETIQUETA_MOTIVO_CIERRE}</label>
            <input
              id="dia-cerrado-motivo"
              ref={motivoRef}
              type="text"
              autoComplete="off"
              className="reservas-input"
              value={motivo}
              onChange={(evento) => { setMotivo(evento.target.value); setErroresServidor({}); }}
              aria-invalid={errores.motivo ? 'true' : undefined}
              aria-describedby={errores.motivo ? 'dia-cerrado-motivo-error' : 'dia-cerrado-motivo-ayuda'}
            />
            <span id="dia-cerrado-motivo-ayuda" className="reservas-campo-ayuda">{motivo.length}/{MAXIMO_MOTIVO_DIA_CERRADO}</span>
            {errores.motivo && (
              <span id="dia-cerrado-motivo-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{errores.motivo}</span>
            )}
          </div>
          <button
            type="submit"
            className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
            disabled={enviando}
          >{enviando ? TEXTO_PROCESANDO : TEXTO_CERRAR_DIA}</button>
        </div>
      </form>

      {dias.length === 0 ? (
        <p className="reservas-campo-ayuda">{TEXTO_SIN_DIAS_CERRADOS}</p>
      ) : (
        <ul className="reservas-personal-lista-config">
          {dias.map((dia) => {
            const texto = formatearFechaCorta(dia.fecha);
            return (
              <li key={dia.id} className="reservas-personal-fila-config">
                <div className="reservas-personal-fila-config-cabecera">
                  <span>
                    <strong className="reservas-personal-dia">{texto}</strong>
                    {dia.motivo && <span className="reservas-personal-motivo"> · {dia.motivo}</span>}
                  </span>
                  {quitando === dia.id ? (
                    <span className="reservas-personal-confirmacion-botones" role="group" aria-label={textoPreguntaQuitar(texto)}>
                      <span className="reservas-personal-pregunta">{textoPreguntaQuitar(texto)}</span>
                      <button
                        type="button"
                        className="reservas-boton reservas-boton--chico reservas-boton--peligro reservas-personal-boton-auto"
                        onClick={() => quitar(dia)}
                        disabled={enviando}
                      >{TEXTO_CONFIRMAR_QUITAR}</button>
                      <button
                        type="button"
                        className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
                        onClick={() => setQuitando(null)}
                        disabled={enviando}
                      >{TEXTO_CANCELAR}</button>
                    </span>
                  ) : (
                    <button
                      type="button"
                      className="reservas-boton reservas-boton--chico reservas-boton--fantasma-error reservas-personal-boton-auto"
                      onClick={() => setQuitando(dia.id)}
                      aria-label={`${TEXTO_QUITAR_CIERRE} el cierre del ${texto}`}
                    >{TEXTO_QUITAR_CIERRE}</button>
                  )}
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
