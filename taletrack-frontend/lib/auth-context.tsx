'use client';

import { createContext, useContext, useState, useEffect } from 'react';
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

type AuthState = {
  isAuthenticated: boolean;
  loading: boolean;
  user: AuthUser | null;
};

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<AuthState>({ isAuthenticated: false, loading: true, user: null });

  useEffect(() => {
    const token = localStorage.getItem('token');
    if (token) {
      const maxAge = 60 * 60 * 24 * 30;
      document.cookie = `tt-token=${token}; path=/; SameSite=Lax; max-age=${maxAge}`;
      const decoded = parseJwt(token);
      setState({
        isAuthenticated: true,
        loading: false,
        user: {
          id: parseInt(decoded?.sub ?? '0'),
          username: decoded?.unique_name ?? 'User',
          email: decoded?.email ?? '',
        },
      });
    } else {
      setState({ isAuthenticated: false, loading: false, user: null });
    }
  }, []);

  const login = (u: AuthUser) => setState({ isAuthenticated: true, loading: false, user: u });

  const logout = () => {
    authService.logout();
    setState({ isAuthenticated: false, loading: false, user: null });
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
