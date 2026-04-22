'use client';

import { createContext, useContext, useEffect, useState } from 'react';
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
  loading: true,
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
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [loading, setLoading] = useState(true);
  const [user, setUser] = useState<AuthUser | null>(null);

  useEffect(() => {
    const token = localStorage.getItem('token');
    if (token) {
      const decoded = parseJwt(token);
      setIsAuthenticated(true);
      setUser({
        username: decoded?.username ?? 'User',
        email: decoded?.email ?? '',
      });
    }
    setLoading(false);
  }, []);

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
    <AuthContext.Provider value={{ isAuthenticated, loading, user, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}
