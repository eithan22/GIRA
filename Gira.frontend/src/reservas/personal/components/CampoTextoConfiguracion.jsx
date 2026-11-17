/**
 * Campo de texto de Configuración con etiqueta visible y ayuda, hermano de CampoNumericoConfiguracion.jsx: mismas
 * clases y misma estructura (label + input + ayuda + error), pero sin type="number", sin unidad y sin texto
 * legible, porque su valor no es un número que se pueda convertir a horas ni a días. El error del campo se enlaza
 * con aria-describedby y aria-invalid. La ayuda se muestra siempre, también con el campo vacío.
 */
export default function CampoTextoConfiguracion({
  id, etiqueta, ayuda, valor, error, onCambiar, inputRef,
  tipo = 'text', modoEntrada, autoCompletar = 'off', maximoCaracteres,
}) {
  const describedBy = [ayuda ? `${id}-ayuda` : null, error ? `${id}-error` : null].filter(Boolean).join(' ');

  return (
    <div className="reservas-campo">
      <label className="reservas-campo-etiqueta" htmlFor={id}>{etiqueta}</label>
      <input
        id={id}
        ref={inputRef}
        type={tipo}
        inputMode={modoEntrada}
        autoComplete={autoCompletar}
        maxLength={maximoCaracteres}
        className="reservas-input"
        value={valor}
        onChange={(evento) => onCambiar(evento.target.value)}
        aria-invalid={error ? 'true' : undefined}
        aria-describedby={describedBy || undefined}
      />
      {ayuda && <span id={`${id}-ayuda`} className="reservas-campo-ayuda">{ayuda}</span>}
      {error && <span id={`${id}-error`} className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{error}</span>}
    </div>
  );
}
