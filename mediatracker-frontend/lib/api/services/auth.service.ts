import { apiClient } from '../client';
import type { LoginRequest, LoginResponse, RegisterRequest, RegisterResponse } from '../../types';

export const authService = {
  async login(email: string, password: string): Promise<LoginResponse> {
    const response = await apiClient.post<LoginResponse>(
      '/login',
      { email, password } as LoginRequest,
      false,
      false
    );
    
    if (response.success && response.token) {
      apiClient.setToken(response.token);
    }
    
    return response;
  },

  async register(email: string, username: string, password: string): Promise<RegisterResponse> {
    return apiClient.post<RegisterResponse>(
      '/register',
      { email, username, password } as RegisterRequest,
      false,
      true // Requiere API key interna
    );
  },

  logout(): void {
    apiClient.clearToken();
  },

  isAuthenticated(): boolean {
    if (typeof window === 'undefined') return false;
    return !!localStorage.getItem('token');
  },
};
