import { useCallback, useEffect, useRef, useState } from 'react';
import Aviso from '../../components/Aviso.jsx';
import { errorPorCampo } from '../../reservasErrores.js';
import { useCargaConfiguracion } from '../hooks/useCargaConfiguracion.js';
import { invalidarParametrosOperativos } from '../hooks/useParametrosOperativos.js';
import {
  DIAS_HORARIO_ORDEN,
  NOMBRES_DIA,
  armarCuerpoHorario,
  horarioAFormulario,
  horariosIguales,
  validarHorarioDia,
} from '../reservasPersonalFormato.js';
import { mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';
import {
  ETIQUETA_ABIERTO,
  ETIQUETA_APERTURA,
  ETIQUETA_CIERRE,
  INTRO_HORARIO,
  TEXTO_CARGANDO_CONFIGURACION,
  TEXTO_CONFIGURACION_NO_CARGO,
  TEXTO_DIA_CERRADO,
  TEXTO_GUARDANDO,
  TEXTO_GUARDAR,
  exitoHorario,
} from '../reservasPersonalTextos.js';
import EstadoPagina from './EstadoPagina.jsx';

/**
 * Pestaña «Horario» (GI-226, RF-CFG-02): una fila por día, de lunes a domingo (DIAS_HORARIO_ORDEN; el servidor usa
 * DayOfWeek de .NET, 0 = domingo). Cada fila se guarda sola. El 409 de «la franja quedaría fuera del horario» se
 * muestra tal cual, con la franja y el día; al cerrar un día se muestra el aviso de reservas ya hechas.
 */
export default function SeccionHorario({ onSucio }) {
  const carga = useCargaConfiguracion((opciones) => reservasPersonalService.listarHorario(opciones));
  const sucios = useRef(new Set());

  const marcarDia = useCallback((dia, sucio) => {
    if (sucio) sucios.current.add(dia);
    else sucios.current.delete(dia);
    onSucio(sucios.current.size > 0);
  }, [onSucio]);

  if (carga.estado === 'cargando') return <EstadoPagina estado="cargando" mensaje={TEXTO_CARGANDO_CONFIGURACION} />;
  if (carga.estado === 'error') {
    return <EstadoPagina estado="error" mensaje={carga.error || TEXTO_CONFIGURACION_NO_CARGO} onReintentar={carga.recargar} />;
  }

  const porDia = Object.fromEntries((carga.datos ?? []).map((horario) => [horario.diaSemana, horario]));
  return (
    <div className="reservas-personal-seccion reservas-personal-config">
      <p className="reservas-personal-intro">{INTRO_HORARIO}</p>
      <ul className="reservas-personal-lista-config">
        {DIAS_HORARIO_ORDEN.filter((dia) => porDia[dia]).map((dia) => (
          <FilaHorario key={dia} horario={porDia[dia]} onSucio={marcarDia} />
        ))}
      </ul>
    </div>
  );
}

function FilaHorario({ horario, onSucio }) {
  const [base, setBase] = useState(() => horarioAFormulario(horario));
  const [formulario, setFormulario] = useState(base);
  const [intentado, setIntentado] = useState(false);
  const [erroresServidor, setErroresServidor] = useState({});
  const [error, setError] = useState('');
  const [exito, setExito] = useState(null); // { advertencias }
  const [enviando, setEnviando] = useState(false);
  const envioEnCursoRef = useRef(false);
  const aperturaRef = useRef(null);
  const cierreRef = useRef(null);
  const avisoRef = useRef(null);
  const dia = horario.diaSemana;
  const nombre = NOMBRES_DIA[dia];

  const hayCambios = !horariosIguales(formulario, base);
  const errores = { ...(intentado ? validarHorarioDia(formulario) : {}), ...erroresServidor };

  useEffect(() => {
    onSucio(dia, hayCambios);
    return () => onSucio(dia, false);
  }, [dia, hayCambios, onSucio]);

  useEffect(() => {
    if (exito) avisoRef.current?.focus();
  }, [exito]);

  function cambiar(parcial) {
    setFormulario((actual) => ({ ...actual, ...parcial }));
    setErroresServidor({});
    setError('');
    setExito(null);
  }

  async function guardar() {
    if (envioEnCursoRef.current) return;
    setIntentado(true);
    setError('');
    setExito(null);
    const invalidos = validarHorarioDia(formulario);
    if (Object.keys(invalidos).length > 0) {
      setErroresServidor({});
      (invalidos.horaCierre ? cierreRef : aperturaRef).current?.focus();
      return;
    }

    envioEnCursoRef.current = true;
    setEnviando(true);
    try {
      const resultado = await reservasPersonalService.guardarHorarioDia(dia, armarCuerpoHorario(formulario));
      const guardado = horarioAFormulario(resultado.horario);
      setBase(guardado);
      setFormulario(guardado);
      setErroresServidor({});
      setIntentado(false);
      setExito({ advertencias: resultado.advertencias ?? [] });
      invalidarParametrosOperativos();
    } catch (fallo) {
      const campos = errorPorCampo(fallo);
      if (Object.keys(campos).length > 0) setErroresServidor(campos);
      else setError(mensajeDeError(fallo)); // incluye el 409 de franja fuera del horario, con franja y día
    } finally {
      envioEnCursoRef.current = false;
      setEnviando(false);
    }
  }

  const idBase = `horario-${dia}`;
  return (
    <li className="reservas-personal-fila-config">
      <div className="reservas-personal-fila-config-cabecera">
        <strong className="reservas-personal-dia">{nombre}</strong>
        <label className="reservas-personal-casilla" htmlFor={`${idBase}-abierto`}>
          <input
            id={`${idBase}-abierto`}
            type="checkbox"
            checked={formulario.abierto}
            onChange={(evento) => cambiar({ abierto: evento.target.checked })}
          />
          {formulario.abierto ? ETIQUETA_ABIERTO : TEXTO_DIA_CERRADO}
        </label>
      </div>
      <div className="reservas-personal-fila-campos">
        <div className="reservas-campo">
          <label className="reservas-campo-etiqueta" htmlFor={`${idBase}-apertura`}>{ETIQUETA_APERTURA}</label>
          <input
            id={`${idBase}-apertura`}
            ref={aperturaRef}
            type="time"
            step={60}
            className="reservas-input"
            value={formulario.apertura}
            disabled={!formulario.abierto}
            onChange={(evento) => cambiar({ apertura: evento.target.value })}
            aria-invalid={errores.horaApertura ? 'true' : undefined}
            aria-describedby={errores.horaApertura ? `${idBase}-error-apertura` : undefined}
          />
          {errores.horaApertura && (
            <span id={`${idBase}-error-apertura`} className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">
              {errores.horaApertura}
            </span>
          )}
        </div>
        <div className="reservas-campo">
          <label className="reservas-campo-etiqueta" htmlFor={`${idBase}-cierre`}>{ETIQUETA_CIERRE}</label>
          <input
            id={`${idBase}-cierre`}
            ref={cierreRef}
            type="time"
            step={60}
            className="reservas-input"
            value={formulario.cierre}
            disabled={!formulario.abierto}
            onChange={(evento) => cambiar({ cierre: evento.target.value })}
            aria-invalid={errores.horaCierre ? 'true' : undefined}
            aria-describedby={errores.horaCierre ? `${idBase}-error-cierre` : undefined}
          />
          {errores.horaCierre && (
            <span id={`${idBase}-error-cierre`} className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">
              {errores.horaCierre}
            </span>
          )}
        </div>
        <button
          type="button"
          className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
          onClick={guardar}
          disabled={enviando || !hayCambios}
          aria-label={`Guardar horario del ${nombre.toLowerCase()}`}
        >{enviando ? TEXTO_GUARDANDO : TEXTO_GUARDAR}</button>
      </div>
      {error && <Aviso tipo="error">{error}</Aviso>}
      {exito && (
        <div ref={avisoRef} tabIndex={-1}>
          <Aviso tipo="exito" rol="none">{exitoHorario(nombre)}</Aviso>
          {exito.advertencias.map((texto) => <Aviso key={texto} tipo="advertencia">{texto}</Aviso>)}
        </div>
      )}
    </li>
  );
}
