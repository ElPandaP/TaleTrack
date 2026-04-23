'use client';

import { createContext, useContext, useState } from 'react';
import { authService } from '@/lib/api/services';

export interface AuthUser {
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
  loading: false,
  user: null,
  login: () => {},
  logout: () => {},
});

export function parseJwt(token: string): { email?: string; username?: string } | null {
  try {
    return JSON.parse(atob(token.split('.')[1]));
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(() => {
    if (typeof window === 'undefined') return false;
    const token = localStorage.getItem('token');
    if (token) {
      // Sync pre-existing localStorage token to cookie for SSR middleware
      const maxAge = 60 * 60 * 24 * 30;
      document.cookie = `tt-token=${token}; path=/; SameSite=Lax; max-age=${maxAge}`;
    }
    return !!token;
  });

  const [user, setUser] = useState<AuthUser | null>(() => {
    if (typeof window === 'undefined') return null;
    const token = localStorage.getItem('token');
    if (!token) return null;
    const decoded = parseJwt(token);
    return { username: decoded?.username ?? 'User', email: decoded?.email ?? '' };
  });

  const login = (u: AuthUser) => {
    setIsAuthenticated(true);
    setUser(u);
  };

  const logout = () => {
    authService.logout();
    setIsAuthenticated(false);
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ isAuthenticated, loading: false, user, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}
