import { useCallback, useEffect, useRef, useState } from 'react';
import Aviso from '../../components/Aviso.jsx';
import { formatearCodigoVisual, formatearFechaCorta, formatearHora } from '../../reservasFormato.js';
import {
  detalleAFila,
  etiquetaOrigen,
  formatearFechaHoraSantoDomingo,
  formatearTelefono,
  hoyEnSantoDomingo,
  puedeMarcarNoAsistio,
} from '../reservasPersonalFormato.js';
import { esPeticionCancelada, mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';
import {
  AVISO_NO_ASISTIO,
  EXITO_CANCELAR,
  EXITO_CHECKIN,
  EXITO_COMPLETAR,
  EXITO_MODIFICAR,
  EXITO_NO_ASISTIO,
  TEXTO_DETALLE_NO_CARGO,
  TEXTO_REINTENTAR,
  TEXTO_RESERVA_NO_EXISTE,
  TITULO_MODIFICAR,
} from '../reservasPersonalTextos.js';
import { useAhora } from '../hooks/useAhora.js';
import AccionesReserva from './AccionesReserva.jsx';
import ConfirmarDescartar from './ConfirmarDescartar.jsx';
import EstadoReservaPill from './EstadoReservaPill.jsx';
import FormularioModificar from './FormularioModificar.jsx';
import PanelPersonal from './PanelPersonal.jsx';

const EXITOS = {
  checkin: EXITO_CHECKIN,
  completar: EXITO_COMPLETAR,
  noasistio: EXITO_NO_ASISTIO,
  cancelar: EXITO_CANCELAR,
  modificar: EXITO_MODIFICAR,
};

function Dato({ etiqueta, children, ancho }) {
  return (
    <div className={`reservas-personal-dato${ancho ? ' reservas-personal-dato--ancho' : ''}`}>
      <dt>{etiqueta}</dt>
      <dd>{children}</dd>
    </div>
  );
}

/**
 * Panel de detalle de una reserva. Abre al instante con los datos de la fila (`filaInicial`) y
 * carga el detalle completo. Es dueño de su reserva: si la lista se refresca y la fila desaparece,
 * el panel sigue abierto con su propia copia y con lo que el usuario escribió; no se cierra solo.
 * El servidor decide si una acción procede: sus mensajes se muestran tal cual, y ante un 409 se
 * vuelve a pedir el detalle para dejar la fila y el panel al día. Tras cualquier acción aceptada
 * (incluida «Modificar») se actualiza la fila y el panel con la respuesta, sin recargar la lista.
 * `filaInicial` puede ser parcial (desde el mapa de mesas, solo con el id): sin código, teléfono ni
 * origen, y sin estado en una mesa inactiva. El panel muestra «—», no ofrece acciones hasta tener
 * el estado y completa todo al cargar el detalle.
 */
export default function PanelDetalleReserva({
  filaInicial, franjas, franjasFallaron, origenRef, fallbackFocoRef, onCerrar, onActualizada, onNoExiste,
}) {
  const id = filaInicial.id;
  const [hoy] = useState(() => hoyEnSantoDomingo());
  // Hoy y hora de Santo Domingo (Intl), al minuto: deciden si se ofrece «Marcar No asistió».
  const ahora = useAhora();
  const [detalle, setDetalle] = useState(null);
  const [carga, setCarga] = useState('cargando'); // 'cargando' | 'listo' | 'error'
  const [errorCarga, setErrorCarga] = useState('');
  const [intento, setIntento] = useState(0);
  const [aviso, setAviso] = useState(null); // { tipo: 'exito' | 'error', texto }
  const [enviando, setEnviando] = useState(false);
  const [noExiste, setNoExiste] = useState(false);
  const [vista, setVista] = useState('detalle'); // 'detalle' | 'modificar'
  const [borradorModificar, setBorradorModificar] = useState(false);
  const [descartando, setDescartando] = useState(false);

  const envioEnCursoRef = useRef(false);
  const avisoRef = useRef(null);

  // Detalle completo (capacidad de mesa, check-in, motivo, creada en).
  useEffect(() => {
    const controlador = new AbortController();
    setCarga('cargando');
    reservasPersonalService.obtenerReserva(id, { signal: controlador.signal })
      .then((resultado) => {
        setDetalle(resultado);
        setCarga('listo');
      })
      .catch((error) => {
        if (esPeticionCancelada(error)) return;
        if (error.response?.status === 404) {
          setNoExiste(true);
          setCarga('listo');
          onNoExiste();
          return;
        }
        setErrorCarga(mensajeDeError(error, TEXTO_DETALLE_NO_CARGO));
        setCarga('error');
      });
    return () => controlador.abort();
    // onNoExiste es estable en la página; solo se vuelve a pedir si cambia la reserva o se reintenta.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, intento]);

  // Tras un éxito el foco pasa al aviso (es lo que cambió); un error se anuncia con role="alert".
  useEffect(() => {
    if (aviso?.tipo === 'exito') avisoRef.current?.focus();
  }, [aviso]);

  const refrescarDetalle = useCallback(async () => {
    try {
      const actual = await reservasPersonalService.obtenerReserva(id);
      setDetalle(actual);
      onActualizada(actual);
    } catch (error) {
      if (error.response?.status === 404) {
        setNoExiste(true);
        onNoExiste();
      }
      // Cualquier otro fallo: se deja el panel como estaba; el aviso del 409 ya explica qué pasó.
    }
  }, [id, onActualizada, onNoExiste]);

  async function ejecutar(tipo, datos) {
    if (envioEnCursoRef.current) return false;
    envioEnCursoRef.current = true;
    setEnviando(true);
    setAviso(null);
    try {
      let resultado;
      if (tipo === 'checkin') resultado = await reservasPersonalService.checkIn(id, datos.personasReales);
      else if (tipo === 'completar') resultado = await reservasPersonalService.completar(id);
      else if (tipo === 'noasistio') resultado = await reservasPersonalService.marcarNoAsistio(id);
      else if (tipo === 'modificar') resultado = await reservasPersonalService.modificar(id, datos);
      else resultado = await reservasPersonalService.cancelar(id, datos.motivo);

      // La respuesta es el detalle actualizado: se refresca el panel y solo esa fila de la lista.
      setDetalle(resultado);
      onActualizada(resultado);
      setAviso({ tipo: 'exito', texto: EXITOS[tipo] });
      if (tipo === 'modificar') setVista('detalle');
      return true;
    } catch (error) {
      const estado = error.response?.status;
      setAviso({ tipo: 'error', texto: mensajeDeError(error) });
      if (estado === 404) {
        setNoExiste(true);
        onNoExiste();
      } else if (estado === 409) {
        await refrescarDetalle();
      }
      return false;
    } finally {
      envioEnCursoRef.current = false;
      setEnviando(false);
    }
  }

  const fila = detalle ? detalleAFila(detalle) : filaInicial;
  const horas = `${formatearHora(fila.franjaHoraInicio)}–${formatearHora(fila.franjaHoraFin)}`;
  // Si el estado cambió (por ejemplo tras un 409) y ya no es Confirmada, la edición se cierra sola.
  const modificando = vista === 'modificar' && Boolean(detalle) && fila.estado === 'Confirmada' && !noExiste;

  // `descartando` guarda qué se descartaría: el panel entero ('cerrar') o solo la edición ('volver').
  function intentarCerrar() {
    if (descartando) {
      setDescartando(false);
      return;
    }
    if (modificando && borradorModificar) setDescartando('cerrar');
    else onCerrar();
  }

  function descartar() {
    if (descartando === 'volver') {
      setDescartando(false);
      setVista('detalle');
    } else {
      onCerrar();
    }
  }

  const bloqueAviso = aviso && (
    <div className="reservas-personal-panel-bloque" ref={avisoRef} tabIndex={-1}>
      {/* El éxito recibe el foco: su Aviso va sin rol de región en vivo para no leerlo dos veces. */}
      <Aviso tipo={aviso.tipo === 'exito' ? 'exito' : 'error'} rol={aviso.tipo === 'exito' ? 'none' : undefined}>{aviso.texto}</Aviso>
    </div>
  );

  return (
    <PanelPersonal
      titulo={modificando ? TITULO_MODIFICAR : (fila.codigo ? `Reserva ${formatearCodigoVisual(fila.codigo)}` : 'Reserva')}
      tituloId="panel-reserva-titulo"
      eyebrow={modificando && fila.codigo ? `RESERVA ${formatearCodigoVisual(fila.codigo)}` : 'DETALLE'}
      onCerrar={intentarCerrar}
      bloqueado={enviando}
      origenRef={origenRef}
      fallbackFocoRef={fallbackFocoRef}
    >
      {descartando && <ConfirmarDescartar onSeguir={() => setDescartando(false)} onDescartar={descartar} />}

      {fila.estado && (
        <div className="reservas-personal-panel-estado">
          <EstadoReservaPill estado={fila.estado} />
        </div>
      )}

      {carga === 'error' && (
        <div className="reservas-personal-panel-bloque">
          <Aviso tipo="error">{errorCarga}</Aviso>
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={() => setIntento((n) => n + 1)}
          >{TEXTO_REINTENTAR}</button>
        </div>
      )}

      {noExiste && (
        <div className="reservas-personal-panel-bloque">
          <Aviso tipo="error">{TEXTO_RESERVA_NO_EXISTE}</Aviso>
        </div>
      )}

      {modificando ? (
        <>
          {bloqueAviso}
          <FormularioModificar
            detalle={detalle}
            franjas={franjas}
            franjasFallaron={franjasFallaron}
            hoy={hoy}
            enviando={enviando}
            onEnviar={(datos) => ejecutar('modificar', datos)}
            onVolver={() => (borradorModificar ? setDescartando('volver') : setVista('detalle'))}
            onBorrador={setBorradorModificar}
          />
        </>
      ) : (
        <>
          <dl className="reservas-personal-datos">
            <Dato etiqueta="Cliente">{fila.clienteNombre}</Dato>
            <Dato etiqueta="Teléfono">{formatearTelefono(fila.clienteTelefono) || '—'}</Dato>
            <Dato etiqueta="Fecha">{formatearFechaCorta(fila.fecha)}</Dato>
            <Dato etiqueta="Franja">{fila.franjaNombre} · {horas}</Dato>
            <Dato etiqueta="Mesa">
              Mesa {fila.mesaNumero} · {fila.zonaNombre}
              {detalle ? ` · capacidad ${detalle.mesaCapacidad}` : ''}
            </Dato>
            <Dato etiqueta="Origen">{etiquetaOrigen(fila.origen) || '—'}</Dato>
            <Dato etiqueta="Personas reservadas">{fila.personas ?? '—'}</Dato>
            <Dato etiqueta="Personas que llegaron">{fila.personasReales != null ? fila.personasReales : '—'}</Dato>
            {detalle && (
              <>
                <Dato etiqueta="Check-in">{detalle.fechaCheckIn ? formatearFechaHoraSantoDomingo(detalle.fechaCheckIn) : '—'}</Dato>
                <Dato etiqueta="Creada">{formatearFechaHoraSantoDomingo(detalle.creadaEn) || '—'}</Dato>
                {detalle.motivoCancelacion && <Dato etiqueta="Motivo de la cancelación" ancho>{detalle.motivoCancelacion}</Dato>}
              </>
            )}
          </dl>

          {carga === 'cargando' && <p className="reservas-personal-panel-cargando" role="status">Cargando el detalle…</p>}

          {fila.estado === 'NoAsistio' && (
            <div className="reservas-personal-panel-bloque">
              <Aviso tipo="neutro">{AVISO_NO_ASISTIO}</Aviso>
            </div>
          )}

          {bloqueAviso}

          {!noExiste && (
            <AccionesReserva
              estado={fila.estado}
              detalle={detalle}
              personasReserva={fila.personas}
              puedeNoAsistio={puedeMarcarNoAsistio({
                estado: fila.estado,
                fecha: fila.fecha,
                hoy: ahora.hoy,
                hora: ahora.hora,
                franjaHoraInicio: fila.franjaHoraInicio,
              })}
              enviando={enviando}
              onEjecutar={ejecutar}
              onModificar={() => { setAviso(null); setVista('modificar'); }}
            />
          )}
        </>
      )}
    </PanelPersonal>
  );
}
