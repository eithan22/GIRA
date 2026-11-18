import { useEffect, useState } from 'react';
import { useAuth } from '../../security/AuthContext.jsx';
import Icon from '../../security/components/Icon.jsx';
import PaginaInventario, { AvisoError } from '../components/PaginaInventario.jsx';
import { ROLES_ATENDER, TIPOS_MOVIMIENTO, aNumero, armarMovimiento, hoyLocal, mensajeError, numero, tieneRol } from '../inventarioFormato.js';
import { inventarioService, todosLosInsumos } from '../inventarioService.js';
import { useCarga } from '../useCarga.js';

const VACIO = { insumoId: '', tipo: 'Entrada', cantidad: '', sentido: '+', costoUnitario: '', motivo: '', referencia: '', fechaVencimiento: '' };

/**
 * Ruta /inventario/movimientos: registrar entradas, mermas y ajustes (RF-INV-05), y el conteo físico
 * (RF-INV-11, solo Supervisor, Gerente y Administrador). Las salidas por consumo las registra el sistema
 * solo, cuando se completa una visita.
 */
export default function Movimientos() {
  const { user } = useAuth();
  const puedeContar = tieneRol(user, ROLES_ATENDER);
  const [modo, setModo] = useState('movimiento');
  const [insumos, setInsumos] = useState([]);
  const [error, setError] = useState('');
  const { errorCarga, setErrorCarga, reintento, reintentar } = useCarga();

  useEffect(() => {
    let activo = true;
    todosLosInsumos()
      .then((lista) => { if (activo) setInsumos(lista); })
      .catch((e) => { if (activo) setErrorCarga(mensajeError(e, 'No se pudieron cargar los insumos.')); });
    return () => { activo = false; };
  }, [reintento, setErrorCarga]);

  return (
    <PaginaInventario titulo="Movimientos" descripcion="Todo cambio de stock queda registrado con quién lo hizo y por qué.">
      <AvisoError mensaje={error} onCerrar={() => setError('')} />
      <AvisoError mensaje={errorCarga} onReintentar={reintentar} />
      {puedeContar && (
        <div className="inv-segmentado" role="group" aria-label="Tipo de registro">
          <button type="button" className={modo === 'movimiento' ? 'active' : ''} aria-pressed={modo === 'movimiento'} onClick={() => setModo('movimiento')}>Un movimiento</button>
          <button type="button" className={modo === 'conteo' ? 'active' : ''} aria-pressed={modo === 'conteo'} onClick={() => setModo('conteo')}>Conteo físico</button>
        </div>
      )}
      {modo === 'movimiento'
        ? <FormularioMovimiento insumos={insumos} onGuardado={(m) => setInsumos(insumos.map((i) => (i.id === m.insumoId ? { ...i, cantidadStock: m.saldoResultante } : i)))} />
        : <ConteoFisico insumos={insumos} onGuardado={() => todosLosInsumos().then(setInsumos).catch(() => {})} />}
    </PaginaInventario>
  );
}

function FormularioMovimiento({ insumos, onGuardado }) {
  const [form, setForm] = useState(VACIO);
  const [ocupado, setOcupado] = useState(false);
  const [error, setError] = useState('');
  const [hechos, setHechos] = useState([]); // solo los de esta sesión de pantalla
  const insumo = insumos.find((i) => i.id === form.insumoId);
  const pideMotivo = form.tipo === 'Merma' || form.tipo === 'Ajuste';
  const campo = (nombre) => ({ value: form[nombre], onChange: (e) => setForm({ ...form, [nombre]: e.target.value }) });

  async function guardar(evento) {
    evento.preventDefault();
    setOcupado(true);
    setError('');
    try {
      const movimiento = await inventarioService.registrarMovimiento(armarMovimiento(form));
      setHechos([{ ...movimiento, nombre: insumo?.nombre, unidad: insumo?.unidad }, ...hechos].slice(0, 8));
      onGuardado(movimiento);
      setForm({ ...VACIO, tipo: form.tipo });
    } catch (e) {
      setError(mensajeError(e));
    } finally { setOcupado(false); }
  }

  return (
    <div className="inv-dos-columnas">
      <form className="panel inv-panel-form" onSubmit={guardar}>
        <div className="panel-heading"><div><h2>Registrar movimiento</h2><p>Entrada: llegó mercancía. Merma: se dañó o se botó. Ajuste: corregir una diferencia.</p></div></div>
        <div className="inv-form inv-form-panel">
          <AvisoError mensaje={error} onCerrar={() => setError('')} />
          <div className="inv-segmentado" role="group" aria-label="Tipo de movimiento">
            {['Entrada', 'Merma', 'Ajuste'].map((t) => (
              <button key={t} type="button" className={form.tipo === t ? 'active' : ''} aria-pressed={form.tipo === t} onClick={() => setForm({ ...form, tipo: t })}>{TIPOS_MOVIMIENTO[t]}</button>
            ))}
          </div>
          <label className="form-field"><span>Insumo <i>*</i></span>
            <select {...campo('insumoId')} required>
              <option value="">Elige un insumo</option>
              {insumos.map((i) => <option key={i.id} value={i.id}>{i.nombre} ({numero(i.cantidadStock)} {i.unidad})</option>)}
            </select>
          </label>
          <div className="form-grid">
            <label className="form-field"><span>Cantidad {insumo ? `(${insumo.unidad})` : ''} <i>*</i></span><input {...campo('cantidad')} inputMode="decimal" required /></label>
            {form.tipo === 'Ajuste' && (
              <label className="form-field"><span>El ajuste…</span>
                <select {...campo('sentido')}><option value="+">Suma al stock</option><option value="-">Resta del stock</option></select>
              </label>
            )}
            {form.tipo === 'Entrada' && <label className="form-field"><span>Costo por unidad (RD$)</span><input {...campo('costoUnitario')} inputMode="decimal" /></label>}
            {form.tipo === 'Entrada' && insumo?.perecedero && <label className="form-field"><span>Vence el <i>*</i></span><input type="date" min={hoyLocal()} {...campo('fechaVencimiento')} required /></label>}
          </div>
          <label className="form-field"><span>Motivo {pideMotivo && <i>*</i>}</span><input {...campo('motivo')} maxLength={300} required={pideMotivo} placeholder={form.tipo === 'Merma' ? 'Ej. se dañó en la nevera' : ''} /></label>
          <label className="form-field"><span>Referencia</span><input {...campo('referencia')} maxLength={200} placeholder="Ej. factura 0012" /></label>
          {insumo && aNumero(form.cantidad) !== null && (
            <p className="field-hint">Stock después: <strong>{numero(insumo.cantidadStock + (form.tipo === 'Entrada' || (form.tipo === 'Ajuste' && form.sentido === '+') ? 1 : -1) * Math.abs(aNumero(form.cantidad)))} {insumo.unidad}</strong></p>
          )}
          <div className="modal-actions"><button type="submit" className="button button-primary" disabled={ocupado}>{ocupado ? 'Guardando…' : 'Registrar'}</button></div>
        </div>
      </form>
      <div className="panel">
        <div className="panel-heading"><div><h2>Registrados ahora</h2><p>Lo que guardaste en esta pantalla. El historial completo está en Kardex.</p></div></div>
        {hechos.length ? (
          <ul className="inv-hechos">
            {hechos.map((m) => (
              <li key={m.id}><Icon name="check" size={14} /><span><strong>{TIPOS_MOVIMIENTO[m.tipo]}</strong> de {numero(Math.abs(m.cantidad))} {m.unidad} de {m.nombre}<small>Queda: {numero(m.saldoResultante)} {m.unidad}</small></span></li>
            ))}
          </ul>
        ) : <div className="empty-state"><span className="empty-icon"><Icon name="box" size={21} /></span><strong>Todavía no registras nada</strong></div>}
      </div>
    </div>
  );
}

function ConteoFisico({ insumos, onGuardado }) {
  const [contado, setContado] = useState({});
  const [motivo, setMotivo] = useState('Conteo físico');
  const [ocupado, setOcupado] = useState(false);
  const [error, setError] = useState('');
  const [resultado, setResultado] = useState(null);
  const lineas = insumos
    .filter((i) => aNumero(contado[i.id]) !== null)
    .map((i) => ({ insumoId: i.id, cantidadContada: aNumero(contado[i.id]) }));

  async function guardar(evento) {
    evento.preventDefault();
    setOcupado(true);
    setError('');
    try {
      setResultado(await inventarioService.conteoFisico({ motivo, lineas }));
      setContado({});
      onGuardado();
    } catch (e) {
      setError(mensajeError(e));
    } finally { setOcupado(false); }
  }

  return (
    <form className="panel" onSubmit={guardar}>
      <div className="panel-heading"><div><h2>Conteo físico</h2><p>Escribe lo que contaste. Solo se crea un ajuste donde haya diferencia; lo que dejes vacío no se toca.</p></div><span className="count-badge">{lineas.length} contados</span></div>
      <div className="inv-form inv-form-panel">
        <AvisoError mensaje={error} onCerrar={() => setError('')} />
        {resultado && <div className="inv-demanda" role="status"><Icon name="check" size={16} /><span>Listo: se generaron <strong>{resultado.ajustesGenerados}</strong> {resultado.ajustesGenerados === 1 ? 'ajuste' : 'ajustes'}.</span></div>}
        <label className="form-field"><span>Motivo <i>*</i></span><input value={motivo} onChange={(e) => setMotivo(e.target.value)} maxLength={300} required /></label>
      </div>
      <div className="table-scroll">
        <table className="data-table">
          <thead><tr><th>INSUMO</th><th className="inv-num">SISTEMA</th><th>CONTADO</th><th className="inv-num">DIFERENCIA</th></tr></thead>
          <tbody>
            {insumos.map((i) => {
              const valor = aNumero(contado[i.id]);
              const diferencia = valor === null ? null : valor - i.cantidadStock;
              return (
                <tr key={i.id}>
                  <td><strong className="inv-nombre">{i.nombre}</strong></td>
                  <td className="inv-num">{numero(i.cantidadStock)} {i.unidad}</td>
                  <td><input className="inv-input-tabla" inputMode="decimal" aria-label={`Contado de ${i.nombre}`} value={contado[i.id] ?? ''} onChange={(e) => setContado({ ...contado, [i.id]: e.target.value })} /></td>
                  <td className={`inv-num${diferencia ? (diferencia < 0 ? ' inv-negativo' : ' inv-positivo') : ''}`}>{diferencia === null ? '—' : `${diferencia > 0 ? '+' : ''}${numero(diferencia)} ${i.unidad}`}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      <div className="form-actions inv-acciones-pie"><button type="submit" className="button button-primary" disabled={ocupado || !lineas.length}>{ocupado ? 'Guardando…' : 'Guardar conteo'}</button></div>
    </form>
  );
}
