'use client';

import { createContext, useContext, useEffect, useRef, useSyncExternalStore } from 'react';
import { authService } from '@/lib/api/services';
import { apiClient } from '@/lib/api/client';

export interface AuthUser {
  id: number;
  username: string;
  email: string;
}

interface AuthContextType {
  isAuthenticated: boolean;
  loading: boolean;
  user: AuthUser | null;
  login: (user: AuthUser) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType>({
  isAuthenticated: false,
  loading: true,
  user: null,
  login: () => {},
  logout: () => {},
});

export function parseJwt(
  token: string,
): { email?: string; unique_name?: string; sub?: string; exp?: number } | null {
  try {
    return JSON.parse(atob(token.split('.')[1]));
  } catch {
    return null;
  }
}

function isExpired(decoded: { exp?: number } | null): boolean {
  return !decoded || typeof decoded.exp !== 'number' || decoded.exp * 1000 <= Date.now();
}

// --- External store ---

const authListeners = new Set<() => void>();

function notifyAuthChange() {
  authListeners.forEach((fn) => fn());
}

function subscribeAuth(callback: () => void) {
  authListeners.add(callback);
  return () => authListeners.delete(callback);
}

type AuthSnapshot =
  | { isAuthenticated: false; loading: false; user: null }
  | { isAuthenticated: false; loading: true; user: null }
  | { isAuthenticated: true; loading: false; user: AuthUser };

const UNAUTHENTICATED: AuthSnapshot = { isAuthenticated: false, loading: false, user: null };
const REFRESHING: AuthSnapshot = { isAuthenticated: false, loading: true, user: null };

let cachedToken: string | null | undefined = undefined;
let cachedSnapshot: AuthSnapshot = UNAUTHENTICATED;

function getAuthSnapshot(): AuthSnapshot {
  const token = localStorage.getItem('token');
  if (token === cachedToken) return cachedSnapshot;
  cachedToken = token;
  if (!token) {
    cachedSnapshot = UNAUTHENTICATED;
    return cachedSnapshot;
  }
  const decoded = parseJwt(token);
  if (isExpired(decoded)) {
    // Access token is dead. If we hold a refresh token, sit in a loading state
    // while AuthProvider's effect swaps it for a fresh one; otherwise log out.
    if (localStorage.getItem('tt-refresh')) {
      cachedSnapshot = REFRESHING;
      return cachedSnapshot;
    }
    localStorage.removeItem('token');
    document.cookie = 'tt-token=; path=/; max-age=0';
    cachedToken = null;
    cachedSnapshot = UNAUTHENTICATED;
    return cachedSnapshot;
  }
  cachedSnapshot = {
    isAuthenticated: true,
    loading: false,
    user: {
      id: parseInt(decoded?.sub ?? '0'),
      username: decoded?.unique_name ?? 'User',
      email: decoded?.email ?? '',
    },
  };
  return cachedSnapshot;
}

function getServerAuthSnapshot() {
  return UNAUTHENTICATED;
}

// --- Provider ---

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const state = useSyncExternalStore(subscribeAuth, getAuthSnapshot, getServerAuthSnapshot);
  const refreshing = useRef(false);

  // Access token expired but a refresh token is present: exchange it once.
  // Any failure (rejected or unreachable) drops to logged-out rather than spin.
  useEffect(() => {
    if (!state.loading || refreshing.current) return;
    refreshing.current = true;
    apiClient
      .refresh()
      .then((ok) => {
        if (!ok) apiClient.clearToken();
      })
      .finally(() => {
        refreshing.current = false;
        notifyAuthChange();
      });
  }, [state.loading]);

  const login = () => {
    const token = localStorage.getItem('token');
    if (token) {
      const maxAge = 60 * 60 * 24 * 30;
      document.cookie = `tt-token=${token}; path=/; SameSite=Lax; max-age=${maxAge}`;
    }
    notifyAuthChange();
  };

  const logout = () => {
    authService.logout();
    document.cookie = 'tt-token=; path=/; max-age=0';
    notifyAuthChange();
  };

  return (
    <AuthContext.Provider value={{ ...state, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}
