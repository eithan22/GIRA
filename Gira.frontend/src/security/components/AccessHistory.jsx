import Icon from './Icon.jsx';

export default function AccessHistory({ accesses = [] }) {
  if (!accesses.length) return <div className="empty-state compact-empty"><span className="empty-icon"><Icon name="clock" size={21} /></span><strong>Sin accesos recientes</strong><p>Los intentos de inicio de sesión aparecerán aquí.</p></div>;
  return (
    <div className="table-scroll">
      <table className="data-table access-table">
        <thead><tr><th>FECHA Y HORA</th><th>DIRECCIÓN IP</th><th>RESULTADO</th></tr></thead>
        <tbody>{accesses.map((access, index) => {
          const success = access.exitoso ?? access.success ?? access.resultado === 'Exitoso';
          const date = access.fecha ?? access.createdAt ?? access.timestamp;
          return (
            <tr className={!success ? 'failed-row' : ''} key={access.id ?? `${date}-${index}`}>
              <td><span className="access-date">{date ? new Date(date).toLocaleString('es-DO', { dateStyle: 'medium', timeStyle: 'short' }) : '—'}</span></td>
              <td className="ip-cell">{access.ip ?? access.direccionIp ?? '—'}</td>
              <td><span className={`status-pill ${success ? 'active' : 'failed'}`}><span className="status-dot" />{success ? 'Exitoso' : 'Fallido'}</span></td>
            </tr>
          );
        })}</tbody>
      </table>
    </div>
  );
}
