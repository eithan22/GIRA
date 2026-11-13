// Agrupa las tres pantallas del personal en un solo módulo. rutasPersonal.jsx lo carga con una única
// importación dinámica, así Vite genera UN chunk para todas (una sola petición) en vez de uno por
// pantalla. No se importa desde ningún otro sitio: mientras App.jsx use las páginas directamente, este
// archivo no entra en el paquete.
export { default as AgendaPersonal } from './pages/AgendaPersonal.jsx';
export { default as MapaPersonal } from './pages/MapaPersonal.jsx';
export { default as ConfiguracionPersonal } from './pages/ConfiguracionPersonal.jsx';
