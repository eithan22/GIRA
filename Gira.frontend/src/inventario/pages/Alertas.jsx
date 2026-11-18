import { useEffect, useState } from 'react';
import Icon from '../../security/components/Icon.jsx';
import PaginaInventario, { AvisoError, Paginacion } from '../components/PaginaInventario.jsx';
import { ESTADOS_ALERTA, TIPOS_ALERTA, fecha, fechaHora, mensajeError, numero } from '../inventarioFormato.js';
import { inventarioService } from '../inventarioService.js';

const PAGE_SIZE = 20;

/** Ruta /inventario/alertas: panel de alertas (RF-INV-09, RF-INV-10) para atender o descartar. */
export default function Alertas() {
  const [estado, setEstado] = useState('Abierta');
  const [page, setPage] = useState(1);
  const [pagina, setPagina] = useState({ items: [], total: 0 });
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState('');
  const [recarga, setRecarga] = useState(0);
  const [ocupado, setOcupado] = useState(null);

  useEffect(() => {
    let activo = true;
    setCargando(true);
    inventarioService.listarAlertas({ estado, page, pageSize: PAGE_SIZE })
      .then((r) => { if (activo) setPagina(r); })
      .catch((e) => { if (activo) setError(mensajeError(e, 'No se pudieron cargar las alertas.')); })
      .finally(() => { if (activo) setCargando(false); });
    return () => { activo = false; };
  }, [estado, page, recarga]);

  async function cerrar(alerta, nuevoEstado) {
    setOcupado(alerta.id);
    try {
      await inventarioService.cambiarEstadoAlerta(alerta.id, nuevoEstado);
      setRecarga(recarga + 1);
    } catch (e) {
      setError(mensajeError(e));
    } finally { setOcupado(null); }
  }

  return (
    <PaginaInventario titulo="Alertas" descripcion="Avisos de stock bajo, de lo que se acabará pronto, de lo que vence y de diferencias.">
      <AvisoError mensaje={error} onCerrar={() => setError('')} />
      <div className="inv-segmentado" role="group" aria-label="Estado de las alertas">
        {Object.entries(ESTADOS_ALERTA).map(([v, t]) => (
          <button key={v} type="button" className={estado === v ? 'active' : ''} aria-pressed={estado === v} onClick={() => { setEstado(v); setPage(1); }}>{t === 'Abierta' ? 'Abiertas' : `${t}s`}</button>
        ))}
      </div>
      <div className="panel">
        <div className="panel-heading"><div><h2>{estado === 'Abierta' ? 'Por atender' : `Alertas ${ESTADOS_ALERTA[estado].toLowerCase()}s`}</h2><p>Una entrada que repone el stock cierra sola las alertas de stock bajo y de quiebre.</p></div><span className="count-badge">{pagina.total}</span></div>
        {cargando ? <p className="table-message"><span className="button-spinner" /> Cargando…</p>
          : !pagina.items.length ? <div className="empty-state"><span className="empty-icon"><Icon name="check" size={21} /></span><strong>No hay alertas aquí</strong></div>
            : (
              <ul className="inv-alertas">
                {pagina.items.map((a) => (
                  <li key={a.id} className={`inv-alerta inv-alerta-${a.tipo}`}>
                    <span className="inv-alerta-icono"><Icon name="alert" size={18} /></span>
                    <div className="inv-alerta-cuerpo">
                      <strong>{TIPOS_ALERTA[a.tipo] ?? a.tipo}: {a.insumo}</strong>
                      <p>
                        Stock ahora: {numero(a.stockInsumo)} {a.unidad} (reorden {numero(a.puntoReorden)} {a.unidad}).
                        {a.fechaQuiebreEstimada && <> Quedará por debajo del reorden el <strong>{fecha(a.fechaQuiebreEstimada)}</strong>.</>}
                        {a.detalle && <> {a.detalle}</>}
                      </p>
                      <small>Generada {fechaHora(a.fechaGeneracion)}{a.atendidaPor && <> · {ESTADOS_ALERTA[a.estado]} por {a.atendidaPor} el {fechaHora(a.fechaAtencion)}</>}</small>
                    </div>
                    {a.estado === 'Abierta' && (
                      <div className="inv-alerta-acciones">
                        <button type="button" className="button button-primary button-small" disabled={ocupado === a.id} onClick={() => cerrar(a, 'Atendida')}>Atendida</button>
                        <button type="button" className="button button-secondary button-small" disabled={ocupado === a.id} onClick={() => cerrar(a, 'Descartada')}>Descartar</button>
                      </div>
                    )}
                  </li>
                ))}
              </ul>
            )}
        <Paginacion page={page} pageSize={PAGE_SIZE} total={pagina.total} cargando={cargando} onCambiar={setPage} />
      </div>
    </PaginaInventario>
  );
}
