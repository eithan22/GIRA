import axios from 'axios';

const baseURL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';
const ACCESS_KEY = 'gira.accessToken';
const REFRESH_KEY = 'gira.refreshToken';
const USER_KEY = 'gira.user';
const DEMO_KEY = 'gira.demoMode';

export const api = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
});

const bareApi = axios.create({ baseURL, withCredentials: true });
let refreshPromise;
let redirecting = false;

export const tokenStore = {
  getAccess: () => localStorage.getItem(ACCESS_KEY),
  getRefresh: () => localStorage.getItem(REFRESH_KEY),
  set({ accessToken, refreshToken }) {
    if (accessToken) localStorage.setItem(ACCESS_KEY, accessToken);
    if (refreshToken) localStorage.setItem(REFRESH_KEY, refreshToken);
  },
  clear() {
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(USER_KEY);
    localStorage.removeItem(DEMO_KEY);
  },
  setUser(user) { localStorage.setItem(USER_KEY, JSON.stringify(user)); },
  getUser() {
    try { return JSON.parse(localStorage.getItem(USER_KEY) || 'null'); } catch { return null; }
  },
  isDemo: () => localStorage.getItem(DEMO_KEY) === 'true',
};

api.interceptors.request.use((config) => {
  const token = tokenStore.getAccess();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

function redirectToLogin() {
  if (redirecting || window.location.pathname === '/login') return;
  redirecting = true;
  tokenStore.clear();
  window.dispatchEvent(new Event('gira:session-expired'));
  const returnUrl = `${window.location.pathname}${window.location.search}${window.location.hash}`;
  window.location.replace(`/login?returnUrl=${encodeURIComponent(returnUrl)}`);
}

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const original = error.config;
    const requestUrl = original?.url || '';
    const isAuthRequest = /\/auth\/(login|refresh|logout)/i.test(requestUrl);
    if (error.response?.status !== 401 || !original || original._retry || isAuthRequest || tokenStore.isDemo()) {
      return Promise.reject(error);
    }

    original._retry = true;
    try {
      if (!refreshPromise) {
        refreshPromise = bareApi.post('/api/auth/refresh', {
          refreshToken: tokenStore.getRefresh(),
        }).then(({ data }) => {
          const result = data?.data ?? data;
          const accessToken = result.accessToken ?? result.token;
          if (!accessToken) throw new Error('La respuesta de renovación no incluye un token de acceso.');
          tokenStore.set({ accessToken, refreshToken: result.refreshToken });
          return accessToken;
        }).finally(() => { refreshPromise = undefined; });
      }
      const accessToken = await refreshPromise;
      original.headers = original.headers || {};
      original.headers.Authorization = `Bearer ${accessToken}`;
      return api(original);
    } catch (refreshError) {
      redirectToLogin();
      return Promise.reject(refreshError);
    }
  },
);

const demoRoles = [
  { name: 'Administrador', description: 'Administra usuarios, roles y accesos.' },
  { name: 'Gerente', description: 'Consulta información de gestión.' },
  { name: 'Supervisor', description: 'Supervisa las operaciones del equipo.' },
  { name: 'Mesero', description: 'Gestiona el servicio de sala.' },
  { name: 'Cocina', description: 'Accede a las herramientas de cocina.' },
];

const demoUsersKey = 'gira.demoUsers';
const demoAccessKey = 'gira.demoAccesses';
const initialUsers = demoRoles.map((role, index) => ({
  id: `demo-${index + 1}`,
  nombre: ['Ana Rodríguez', 'Carlos Méndez', 'Lucía Pérez', 'José Ramírez', 'María Santos'][index],
  email: `${role.name.toLowerCase()}@gira.local`,
  roles: [role.name],
  activo: true,
  debeCambiarPassword: false,
  createdAt: new Date(Date.now() - index * 86400000 * 12).toISOString(),
}));

function getDemoUsers() {
  try {
    const users = JSON.parse(localStorage.getItem(demoUsersKey) || 'null');
    if (Array.isArray(users)) return users;
  } catch { /* Reinitialize corrupted local demo data below. */ }
  localStorage.setItem(demoUsersKey, JSON.stringify(initialUsers));
  return initialUsers;
}

function getDemoAccesses(userId) {
  try {
    const saved = JSON.parse(localStorage.getItem(demoAccessKey) || '{}');
    if (saved[userId]) return saved[userId];
  } catch { /* Use fresh demo access events. */ }
  return [
    { id: 1, fecha: new Date(Date.now() - 1000 * 60 * 18).toISOString(), ip: '192.168.1.24', exitoso: true },
    { id: 2, fecha: new Date(Date.now() - 1000 * 60 * 60 * 3).toISOString(), ip: '192.168.1.24', exitoso: true },
    { id: 3, fecha: new Date(Date.now() - 1000 * 60 * 60 * 8).toISOString(), ip: '10.0.0.58', exitoso: false },
    { id: 4, fecha: new Date(Date.now() - 1000 * 60 * 60 * 23).toISOString(), ip: '192.168.1.24', exitoso: true },
  ];
}

function normalizeUser(raw) {
  const rawRoles = raw.roles ?? (raw.role ? [raw.role] : []);
  return {
    ...raw,
    id: raw.id ?? raw.userId,
    nombre: raw.nombre ?? raw.name ?? raw.fullName ?? raw.email ?? 'Usuario',
    email: raw.email ?? raw.correo ?? '',
    roles: Array.isArray(rawRoles) ? rawRoles.map((role) => typeof role === 'string' ? role : role.name ?? role.nombre).filter(Boolean) : [],
    activo: raw.activo ?? raw.active ?? true,
    debeCambiarPassword: raw.debeCambiarPassword ?? raw.mustChangePassword ?? false,
  };
}

function payload(data) { return data?.data ?? data; }

export const authService = {
  async login(credentials) {
    const result = payload((await api.post('/api/auth/login', credentials)).data);
    const accessToken = result.accessToken ?? result.token;
    if (!accessToken) throw new Error('La respuesta de inicio de sesión no incluye un token de acceso.');
    tokenStore.set({ accessToken, refreshToken: result.refreshToken });
    const user = normalizeUser(result.usuario ?? result.user ?? await this.me());
    tokenStore.setUser(user);
    return { user, accessToken, refreshToken: result.refreshToken };
  },
  async loginDemo(role) {
    const user = getDemoUsers().find((item) => item.roles.includes(role)) || initialUsers[0];
    if (!user.activo) throw new Error('Esta cuenta demo está inactiva. Actívala desde el directorio de usuarios.');
    localStorage.setItem(DEMO_KEY, 'true');
    const loggedInUser = { ...user };
    tokenStore.set({ accessToken: `demo-${Date.now()}`, refreshToken: 'demo-refresh' });
    tokenStore.setUser(loggedInUser);
    const entries = getDemoAccesses(user.id);
    const saved = JSON.parse(localStorage.getItem(demoAccessKey) || '{}');
    saved[user.id] = [{ id: Date.now(), fecha: new Date().toISOString(), ip: '127.0.0.1', exitoso: true }, ...entries];
    localStorage.setItem(demoAccessKey, JSON.stringify(saved));
    return loggedInUser;
  },
  async me() {
    if (tokenStore.isDemo()) return tokenStore.getUser();
    const response = payload((await api.get('/api/auth/me')).data);
    const user = normalizeUser(response.usuario ?? response.user ?? response);
    tokenStore.setUser(user);
    return user;
  },
  async logout() {
    try {
      if (!tokenStore.isDemo() && tokenStore.getAccess()) await api.post('/api/auth/logout', { refreshToken: tokenStore.getRefresh() });
    } finally {
      tokenStore.clear();
    }
  },
  async changePassword({ currentPassword, newPassword }) {
    if (tokenStore.isDemo()) {
      const user = { ...tokenStore.getUser(), debeCambiarPassword: false };
      tokenStore.setUser(user);
      return user;
    }
    const response = payload((await api.post('/api/auth/change-password', { currentPassword, newPassword })).data);
    const user = normalizeUser(response.usuario ?? response.user ?? { ...tokenStore.getUser(), debeCambiarPassword: false });
    tokenStore.setUser(user);
    return user;
  },
  async getRoles() {
    if (tokenStore.isDemo()) return demoRoles;
    return payload((await api.get('/api/roles')).data);
  },
  async getUsers({ page = 1, pageSize = 10, search = '', role = '', status = '' } = {}) {
    if (tokenStore.isDemo()) {
      let items = getDemoUsers();
      if (search) items = items.filter((user) => `${user.nombre} ${user.email}`.toLowerCase().includes(search.toLowerCase()));
      if (role) items = items.filter((user) => user.roles.includes(role));
      if (status) items = items.filter((user) => (status === 'active') === user.activo);
      const totalCount = items.length;
      return { items: items.slice((page - 1) * pageSize, page * pageSize), totalCount, page, pageSize };
    }
    const response = payload((await api.get('/api/users', { params: { page, pageSize, search, role: role || undefined, activo: status === 'active' ? true : status === 'inactive' ? false : undefined } })).data);
    const items = response.items ?? response.users ?? response.data ?? [];
    return { ...response, items: items.map(normalizeUser), totalCount: response.totalCount ?? response.total ?? items.length, page: response.page ?? page, pageSize: response.pageSize ?? pageSize };
  },
  async getUser(id) {
    if (tokenStore.isDemo()) return getDemoUsers().find((user) => user.id === id) || null;
    const response = payload((await api.get(`/api/users/${id}`)).data);
    return normalizeUser(response.user ?? response.usuario ?? response);
  },
  async saveUser(user, id) {
    if (tokenStore.isDemo()) {
      const users = getDemoUsers();
      if (id) {
        const updated = { ...users.find((item) => item.id === id), ...user, id };
        localStorage.setItem(demoUsersKey, JSON.stringify(users.map((item) => item.id === id ? updated : item)));
        return { user: updated };
      }
      const newUser = { ...user, id: `demo-${Date.now()}`, activo: true, debeCambiarPassword: true, createdAt: new Date().toISOString() };
      const temporaryPassword = `Gira-${Math.random().toString(36).slice(2, 7)}!7`;
      localStorage.setItem(demoUsersKey, JSON.stringify([...users, newUser]));
      return { user: newUser, temporaryPassword };
    }
    const response = payload((await (id ? api.put(`/api/users/${id}`, user) : api.post('/api/users', user))).data);
    const temporaryPassword = response.temporaryPassword ?? response.contrasenaTemporal;
    if (!id && !temporaryPassword) throw new Error('El usuario se creó, pero la respuesta no incluye la contraseña temporal. Contacta al administrador antes de compartir el acceso.');
    return { user: normalizeUser(response.user ?? response.usuario ?? response), temporaryPassword };
  },
  async setUserStatus(id, activo) {
    if (tokenStore.isDemo()) {
      const users = getDemoUsers().map((user) => user.id === id ? { ...user, activo } : user);
      localStorage.setItem(demoUsersKey, JSON.stringify(users));
      return users.find((user) => user.id === id);
    }
    const response = payload((await api.patch(`/api/users/${id}/status`, { activo })).data);
    return normalizeUser(response.user ?? response.usuario ?? response);
  },
  async getAccesses(id) {
    if (tokenStore.isDemo()) return getDemoAccesses(id);
    const response = payload((await api.get(`/api/users/${id}/accesses`, { params: { page: 1, pageSize: 10 } })).data);
    const accesses = response.items ?? response.accesos ?? response;
    return Array.isArray(accesses) ? accesses : [];
  },
};
