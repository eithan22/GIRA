import { useEffect, useState } from 'react';
import Icon from '../../security/components/Icon.jsx';
import PaginaInventario, { AvisoError } from '../components/PaginaInventario.jsx';
import { fecha, hoyLocal, mensajeError, numero, sumarDias } from '../inventarioFormato.js';
import { inventarioService } from '../inventarioService.js';
import { useCarga } from '../useCarga.js';

/** Ruta /inventario/proyeccion: consumo esperado por insumo y día según reservas y recetas (RF-INV-07, RF-INV-12). */
export default function Proyeccion() {
  const hoy = hoyLocal();
  const [rango, setRango] = useState({ desde: hoy, hasta: sumarDias(hoy, 6) });
  const [datos, setDatos] = useState(null);
  const [cargando, setCargando] = useState(true);
  const { errorCarga: error, setErrorCarga: setError, reintento, reintentar } = useCarga();

  useEffect(() => {
    let activo = true;
    setCargando(true);
    inventarioService.proyeccion(rango)
      .then((r) => { if (activo) { setDatos(r); setError(''); } })
      .catch((e) => { if (activo) setError(mensajeError(e, 'No se pudo calcular la proyección.')); })
      .finally(() => { if (activo) setCargando(false); });
    return () => { activo = false; };
  }, [rango, reintento, setError]);

  const diaCorto = (iso) => fecha(iso).slice(0, 5);

  return (
    <PaginaInventario titulo="Proyección de consumo" descripcion="Cuánto se va a gastar cada día según las personas con reserva y las recetas.">
      <AvisoError mensaje={error} onReintentar={reintentar} />
      <div className="panel">
        <div className="filter-row inv-filtros inv-filtros-arriba">
          <label className="inv-fecha">Desde <input type="date" value={rango.desde} min={hoy} max={rango.hasta} onChange={(e) => setRango({ ...rango, desde: e.target.value })} /></label>
          <label className="inv-fecha">Hasta <input type="date" value={rango.hasta} min={rango.desde} max={sumarDias(hoy, 13)} onChange={(e) => setRango({ ...rango, hasta: e.target.value })} /></label>
          <span className="field-hint">Hasta 14 días contando hoy. Cada día usa lo mayor entre las personas con reserva y la predicción de Analítica.</span>
        </div>
        {cargando ? <p className="table-message"><span className="button-spinner" /> Calculando…</p> : datos && (
          <div className="table-scroll">
            <table className="data-table inv-proyeccion">
              <thead>
                <tr><th>INSUMO</th><th>STOCK</th>{datos.dias.map((d) => <th key={d.fecha} className="inv-num">{diaCorto(d.fecha)}</th>)}<th>AVISO</th></tr>
                <tr className="inv-fila-personas"><td>Personas esperadas</td><td />{datos.dias.map((d) => <td key={d.fecha} className="inv-num" title={`Con reserva: ${d.conReserva}${d.prediccion == null ? '' : ` · Predicción: ${d.prediccion}`}`}>{d.usados}</td>)}<td /></tr>
              </thead>
              <tbody>
                {!datos.insumos.length
                  ? <tr><td colSpan={datos.dias.length + 3}><div className="empty-state"><span className="empty-icon"><Icon name="box" size={21} /></span><strong>Ningún insumo está en una receta activa</strong></div></td></tr>
                  : datos.insumos.map((i) => (
                    <tr key={i.insumoId}>
                      <td><strong className="inv-nombre">{i.insumo}</strong><small className="inv-sub">Reorden {numero(i.puntoReorden)} {i.unidad}</small></td>
                      <td className="inv-num">{numero(i.stock)} {i.unidad}</td>
                      {i.dias.map((d) => (
                        <td key={d.fecha} className={`inv-num${d.restante <= 0 ? ' inv-celda-agotado' : d.restante < i.puntoReorden ? ' inv-celda-bajo' : ''}`} title={`Queda ${numero(d.restante)} ${i.unidad}`}>
                          {d.cantidad ? numero(d.cantidad) : '·'}
                        </td>
                      ))}
                      <td>{i.fechaAgotamiento ? <span className="inv-nivel inv-nivel-Critico">Se acaba el {diaCorto(i.fechaAgotamiento)}</span>
                        : i.fechaBajoReorden ? <span className="inv-nivel inv-nivel-Bajo">Bajo reorden el {diaCorto(i.fechaBajoReorden)}</span>
                          : <span className="inv-nivel inv-nivel-Normal">Alcanza</span>}</td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        )}
        <p className="field-hint inv-leyenda">Cada celda es lo que se espera gastar ese día. Amarillo: queda por debajo del reorden. Rojo: se acaba. Pasa el cursor para ver cuánto queda.</p>
      </div>
    </PaginaInventario>
  );
}
