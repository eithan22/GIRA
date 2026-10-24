import { useState } from 'react';
import Modal from '../components/Modal.jsx';
import { authService } from '../authService.js';
import Icon from '../components/Icon.jsx';

export default function Register({ onClose, onSuccess }) {
  const [form, setForm] = useState({ nombreCompleto: '', email: '', password: '', confirmacion: '' });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const passwordPolicy = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/.test(form.password);

  async function submit(event) {
    event.preventDefault();
    setError('');
    if (!passwordPolicy) {
      setError('La contraseña debe tener al menos 8 caracteres, una mayúscula, una minúscula, un número y un símbolo.');
      return;
    }
    if (form.password !== form.confirmacion) {
      setError('La contraseña y su confirmación no coinciden.');
      return;
    }

    setLoading(true);
    try {
      await authService.register({
        nombreCompleto: form.nombreCompleto.trim(),
        email: form.email.trim(),
        password: form.password,
      });
      onSuccess(form.email.trim());
    } catch (requestError) {
      const data = requestError.response?.data;
      setError(data?.detail || data?.message || data?.title || 'No se pudo crear la cuenta. Revisa los datos e inténtalo de nuevo.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <Modal title="Crear cuenta" onClose={onClose} wide className="register-modal">
      <p className="register-modal-copy">Completa tus datos para crear una cuenta de personal.</p>
      <form className="register-form" onSubmit={submit}>
        <label className="field-label" htmlFor="nombre-completo">Nombre completo</label>
        <div className="input-wrap"><input id="nombre-completo" type="text" autoComplete="name" placeholder="Tu nombre" value={form.nombreCompleto} onChange={(event) => setForm({ ...form, nombreCompleto: event.target.value })} required minLength={2} maxLength={120} /></div>
        <label className="field-label" htmlFor="registro-email">Correo electrónico</label>
        <div className="input-wrap"><input id="registro-email" type="email" autoComplete="email" placeholder="nombre@empresa.com" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} required maxLength={256} /></div>
        <label className="field-label" htmlFor="registro-password">Contraseña</label>
        <div className="input-wrap"><Icon className="input-leading-icon" name="lock" size={16} /><input id="registro-password" type="password" autoComplete="new-password" placeholder="Crea una contraseña segura" value={form.password} onChange={(event) => setForm({ ...form, password: event.target.value })} required minLength={8} maxLength={128} /></div>
        <label className="field-label" htmlFor="registro-confirmacion">Confirmar contraseña</label>
        <div className="input-wrap"><Icon className="input-leading-icon" name="lock" size={16} /><input id="registro-confirmacion" type="password" autoComplete="new-password" placeholder="Escribe nuevamente la contraseña" value={form.confirmacion} onChange={(event) => setForm({ ...form, confirmacion: event.target.value })} required minLength={8} maxLength={128} /></div>
        <p className="register-role-note">La cuenta se crea con acceso básico de Mesero.</p>
        {error && <div className="form-error" role="alert"><span>!</span>{error}</div>}
        <div className="modal-actions">
          <button type="button" className="button button-secondary" onClick={onClose}>Cancelar</button>
          <button className="button button-primary" type="submit" disabled={loading}>{loading ? <><span className="button-spinner" /> Creando cuenta…</> : <>Crear cuenta <Icon name="arrow" size={16} /></>}</button>
        </div>
      </form>
    </Modal>
  );
}
