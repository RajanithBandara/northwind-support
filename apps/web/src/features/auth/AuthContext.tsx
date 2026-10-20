import { useCallback, useEffect, useMemo, useState } from 'react';
import { authApi } from '../../api/auth';
import {
  clearStoredSession,
  getStoredSession,
  onUnauthorized,
  setStoredSession,
  type StoredSession,
} from '../../api/client';
import type { LoginPayload } from '../../types/api';
import { AuthContext } from './authContextObject';

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<StoredSession | null>(() => getStoredSession());

  useEffect(() => {
    return onUnauthorized(() => {
      setSession(null);
    });
  }, []);

  const login = useCallback(async (payload: LoginPayload) => {
    const response = await authApi.login(payload);
    setStoredSession(response);
    setSession(response);
  }, []);

  const logout = useCallback(() => {
    clearStoredSession();
    setSession(null);
  }, []);

  const value = useMemo(
    () => ({
      agent: session?.agent ?? null,
      token: session?.token ?? null,
      isAuthenticated: Boolean(session?.token && session?.agent),
      login,
      logout,
    }),
    [session, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
