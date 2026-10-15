import axios from 'axios';

const baseURL = import.meta.env.VITE_API_BASE_URL || '/';
const ACCESS_KEY = 'gira.accessToken';
const REFRESH_KEY = 'gira.refreshToken';
const USER_KEY = 'gira.user';
const DEMO_KEY = 'gira.demoMode';

export const api = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
});

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
};

api.interceptors.request.use((config) => {
  const accessToken = tokenStore.getAccess();
  if (accessToken) config.headers.Authorization = `Bearer ${accessToken}`;
  return config;
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    const isLoginRequest = /\/auth\/login(?:\?|$)/i.test(error.config?.url || '');
    if (error.response?.status === 401 && !isLoginRequest && tokenStore.getAccess()) {
      tokenStore.clear();
      window.dispatchEvent(new Event('gira:session-expired'));
    }
    return Promise.reject(error);
  },
);

function normalizeUser(raw) {
  if (!raw || typeof raw !== 'object') {
    throw new Error('La API devolvió una respuesta de usuario inválida.');
  }

  const rawRoles = raw.roles ?? (raw.role ? [raw.role] : []);
  return {
    ...raw,
    id: raw.id ?? raw.userId,
    nombre: raw.nombreCompleto ?? raw.nombre ?? raw.name ?? raw.fullName ?? raw.email ?? 'Usuario',
    email: raw.email ?? raw.correo ?? '',
    roles: Array.isArray(rawRoles) ? rawRoles.map((role) => typeof role === 'string' ? role : role.name ?? role.nombre).filter(Boolean) : [],
    activo: raw.activo ?? raw.active ?? true,
    ultimoAcceso: raw.ultimoAcceso ?? raw.lastAccess ?? null,
  };
}

function problemDetail(error, fallback) {
  return error.response?.data?.detail
    || error.response?.data?.title
    || error.message
    || fallback;
}

function pageResult(data, page, pageSize) {
  const result = data?.data ?? data;
  if (!Array.isArray(result?.items)) {
    throw new Error('La API devolvió un listado de usuarios inválido.');
  }

  return {
    ...result,
    items: result.items.map(normalizeUser),
    totalCount: result.total ?? result.totalCount ?? result.items.length,
    page: result.page ?? page,
    pageSize: result.pageSize ?? pageSize,
  };
}

export const authService = {
  async login(credentials) {
    const response = await api.post('/api/auth/login', credentials);
    const result = response.data?.data ?? response.data;
    const accessToken = result?.accessToken;
    if (!accessToken) throw new Error('La respuesta de inicio de sesión no incluye un token de acceso.');

    tokenStore.set({ accessToken, refreshToken: result.refreshToken });
    const user = normalizeUser(result.usuario ?? await this.me());
    tokenStore.setUser(user);
    return { user, accessToken, refreshToken: result.refreshToken };
  },

  async me() {
    const response = await api.get('/api/auth/me');
    const user = normalizeUser(response.data?.data ?? response.data);
    tokenStore.setUser(user);
    return user;
  },

  async logout() {
    tokenStore.clear();
  },

  async getRoles() {
    const response = await api.get('/api/roles');
    const roles = response.data?.data ?? response.data;
    if (!Array.isArray(roles)) throw new Error('La API devolvió un catálogo de roles inválido.');
    return roles;
  },

  async getUsers({ page = 1, pageSize = 10, role = '', status = '' } = {}) {
    const response = await api.get('/api/usuarios', {
      params: {
        page,
        pageSize,
        rol: role || undefined,
        activo: status === 'active' ? true : status === 'inactive' ? false : undefined,
      },
    });
    return pageResult(response.data, page, pageSize);
  },

  async getUser(id) {
    const firstPage = await this.getUsers({ page: 1, pageSize: 100 });
    const pageCount = Math.ceil(firstPage.totalCount / firstPage.pageSize);
    for (let page = 1; page <= pageCount; page += 1) {
      const result = page === 1
        ? firstPage
        : await this.getUsers({ page, pageSize: firstPage.pageSize });
      const user = result.items.find((item) => item.id === id);
      if (user) return user;
    }
    return null;
  },

  async saveUser(user, id) {
    const request = {
      email: user.email,
      nombreCompleto: user.nombre,
      empleadoId: user.empleadoId ?? null,
    };

    if (id) {
      await api.put(`/api/usuarios/${id}`, request);
      try {
        await api.put(`/api/usuarios/${id}/roles`, { roles: user.roles });
      } catch (error) {
        throw new Error(`Los datos del usuario se guardaron, pero sus roles no se actualizaron: ${problemDetail(error, 'error desconocido')}`);
      }
      return { user: await this.getUser(id) };
    }

    const response = await api.post('/api/usuarios', request);
    const created = response.data?.data ?? response.data;
    const createdUser = normalizeUser(created?.usuario);
    const temporaryPassword = created?.passwordTemporal;
    if (!createdUser.id || !temporaryPassword) {
      throw new Error('La respuesta de creación no incluye el usuario y su contraseña temporal.');
    }

    let roleAssignmentError = '';
    try {
      await api.put(`/api/usuarios/${createdUser.id}/roles`, { roles: user.roles });
    } catch (error) {
      roleAssignmentError = problemDetail(error, 'error desconocido');
    }
    return { user: createdUser, temporaryPassword, roleAssignmentError };
  },

  async setUserStatus(id, activo) {
    await api.patch(`/api/usuarios/${id}/estado`, { activo });
  },

  async getAccesses(id) {
    const response = await api.get(`/api/usuarios/${id}/accesos`, {
      params: { page: 1, pageSize: 10 },
    });
    const result = response.data?.data ?? response.data;
    if (!Array.isArray(result?.items)) {
      throw new Error('La API devolvió un historial de accesos inválido.');
    }
    return result.items;
  },
};
