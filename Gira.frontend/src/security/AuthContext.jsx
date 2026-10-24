import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { authService, tokenStore } from './authService.js';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [sessionError, setSessionError] = useState('');

  useEffect(() => {
    let active = true;
    const expire = () => { setUser(null); setLoading(false); };
    window.addEventListener('gira:session-expired', expire);
    async function restore() {
      if (localStorage.getItem('gira.demoMode') === 'true') tokenStore.clear();
      if (!tokenStore.getAccess()) {
        setLoading(false);
        return;
      }
      try {
        const restored = await authService.me();
        if (active) setUser(restored);
      } catch {
        const stillHasToken = Boolean(tokenStore.getAccess());
        if (stillHasToken) {
          tokenStore.clear();
          if (active) setSessionError('No se pudo recuperar tu sesión. Inicia sesión nuevamente.');
        }
        if (active) setUser(null);
      } finally {
        if (active) setLoading(false);
      }
    }
    restore();
    return () => {
      active = false;
      window.removeEventListener('gira:session-expired', expire);
    };
  }, []);

  const login = useCallback(async (credentials) => {
    setSessionError('');
    const result = await authService.login(credentials);
    setUser(result.user);
    return result.user;
  }, []);
  const logout = useCallback(async () => {
    try {
      await authService.logout();
    } finally {
      setUser(null);
    }
  }, []);
  const clearUser = useCallback(() => setUser(null), []);
  const updateUser = useCallback((nextUser) => {
    setUser(nextUser);
    tokenStore.setUser(nextUser);
  }, []);

  const value = useMemo(() => ({
    user,
    roles: user?.roles ?? [],
    loading,
    sessionError,
    login,
    logout,
    clearUser,
    updateUser,
  }), [user, loading, sessionError, login, logout, clearUser, updateUser]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth debe utilizarse dentro de AuthProvider.');
  return context;
}
