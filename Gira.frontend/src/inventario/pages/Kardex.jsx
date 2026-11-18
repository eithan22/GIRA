import { useEffect, useState } from 'react';
import PaginaInventario, { AvisoError, FilaEstado, Paginacion } from '../components/PaginaInventario.jsx';
import { TIPOS_MOVIMIENTO, fecha, fechaHora, hoyLocal, mensajeError, numero, sumarDias } from '../inventarioFormato.js';
import { inventarioService, todosLosInsumos } from '../inventarioService.js';
import { useCarga } from '../useCarga.js';

const PAGE_SIZE = 50;

/** Ruta /inventario/kardex: historial de un insumo con el saldo después de cada movimiento (RF-INV-06). */
export default function Kardex() {
  const [insumos, setInsumos] = useState([]);
  const [filtros, setFiltros] = useState(() => ({ insumoId: '', desde: sumarDias(hoyLocal(), -29), hasta: hoyLocal() }));
  const [page, setPage] = useState(1);
  const [kardex, setKardex] = useState(null);
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState('');
  const { errorCarga, setErrorCarga, reintento, reintentar } = useCarga();

  useEffect(() => {
    let activo = true;
    todosLosInsumos()
      .then((lista) => { if (!activo) return; setInsumos(lista); setFiltros((f) => ({ ...f, insumoId: f.insumoId || lista[0]?.id || '' })); })
      .catch((e) => { if (activo) setErrorCarga(mensajeError(e, 'No se pudieron cargar los insumos.')); });
    return () => { activo = false; };
  }, [reintento, setErrorCarga]);

  useEffect(() => {
    if (!filtros.insumoId) return undefined;
    let activo = true;
    setCargando(true);
    inventarioService.kardex({ ...filtros, page, pageSize: PAGE_SIZE })
      .then((r) => { if (activo) { setKardex(r); setErrorCarga(''); } })
      .catch((e) => { if (activo) { setKardex(null); setErrorCarga(mensajeError(e, 'No se pudo cargar el kardex.')); } })
      .finally(() => { if (activo) setCargando(false); });
    return () => { activo = false; };
  }, [filtros, page, reintento, setErrorCarga]);

  function filtrar(cambio) { setFiltros({ ...filtros, ...cambio }); setPage(1); }
  const u = kardex?.unidad ?? '';

  return (
    <PaginaInventario titulo="Kardex" descripcion="Cada entrada y salida de un insumo, en orden, con el stock que quedó.">
      <AvisoError mensaje={error} onCerrar={() => setError('')} />
      <AvisoError mensaje={errorCarga} onReintentar={reintentar} />
      <div className="panel">
        <div className="filter-row inv-filtros inv-filtros-arriba">
          <label className="select-wrap inv-ancho"><span className="sr-only">Insumo</span><select value={filtros.insumoId} onChange={(e) => filtrar({ insumoId: e.target.value })}>{insumos.map((i) => <option key={i.id} value={i.id}>{i.nombre}</option>)}</select></label>
          <label className="inv-fecha">Desde <input type="date" value={filtros.desde} max={filtros.hasta} onChange={(e) => filtrar({ desde: e.target.value })} /></label>
          <label className="inv-fecha">Hasta <input type="date" value={filtros.hasta} min={filtros.desde} onChange={(e) => filtrar({ hasta: e.target.value })} /></label>
        </div>
        {kardex && (
          <div className="inv-totales">
            <div><span>Stock al {fecha(kardex.desde)}</span><strong>{numero(kardex.saldoInicial)} {u}</strong></div>
            <div><span>Entradas</span><strong className="inv-positivo">+{numero(kardex.entradas)} {u}</strong></div>
            <div><span>Consumo</span><strong className="inv-negativo">{numero(kardex.salidas)} {u}</strong></div>
            <div><span>Mermas</span><strong className="inv-negativo">{numero(kardex.mermas)} {u}</strong></div>
            <div><span>Ajustes</span><strong>{numero(kardex.ajustes)} {u}</strong></div>
            <div><span>Stock actual</span><strong>{numero(kardex.stockActual)} {u}</strong></div>
          </div>
        )}
        <div className="table-scroll">
          <table className="data-table">
            <thead><tr><th>FECHA</th><th>TIPO</th><th className="inv-num">CANTIDAD</th><th className="inv-num">QUEDÓ</th><th>MOTIVO / REFERENCIA</th><th>QUIÉN</th></tr></thead>
            <tbody>
              {cargando || !kardex?.movimientos.items.length
                ? <FilaEstado columnas={6} cargando={cargando} vacio="No hay movimientos en esas fechas" />
                : kardex.movimientos.items.map((m) => (
                  <tr key={m.id}>
                    <td className="date-cell">{fechaHora(m.fecha)}</td>
                    <td>{TIPOS_MOVIMIENTO[m.tipo] ?? m.tipo}{m.teorico && <small className="inv-sub">Calculado por receta</small>}</td>
                    <td className={`inv-num ${m.cantidad < 0 ? 'inv-negativo' : 'inv-positivo'}`}>{m.cantidad > 0 ? '+' : ''}{numero(m.cantidad)} {u}</td>
                    <td className="inv-num"><strong>{numero(m.saldoResultante)} {u}</strong></td>
                    <td>{m.motivo ?? ''}{m.motivo && m.referencia ? ' · ' : ''}{m.referencia ?? ''}{!m.motivo && !m.referencia ? '—' : ''}</td>
                    <td>{m.usuario ?? 'Sistema'}</td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
        {kardex && <Paginacion page={page} pageSize={PAGE_SIZE} total={kardex.movimientos.total} cargando={cargando} onCambiar={setPage} />}
      </div>
    </PaginaInventario>
  );
}
