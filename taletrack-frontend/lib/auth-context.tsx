'use client';

import { createContext, useContext, useSyncExternalStore } from 'react';
import { authService } from '@/lib/api/services';

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

export function parseJwt(token: string): { email?: string; unique_name?: string; sub?: string } | null {
  try {
    return JSON.parse(atob(token.split('.')[1]));
  } catch {
    return null;
  }
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
  | { isAuthenticated: true; loading: false; user: AuthUser };

const UNAUTHENTICATED: AuthSnapshot = { isAuthenticated: false, loading: false, user: null };

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
