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
      apiClient.setToken(response.token, response.refreshToken);
    }

    return response;
  },

  async register(
    email: string,
    username: string,
    password: string,
    locale: string
  ): Promise<RegisterResponse> {
    return apiClient.post<RegisterResponse>(
      '/register',
      { email, username, password, locale } as RegisterRequest,
      false,
      true // Requiere API key interna
    );
  },

  async googleLogin(idToken: string, locale: string): Promise<LoginResponse> {
    const response = await apiClient.post<LoginResponse>(
      '/auth/google',
      { idToken, locale },
      false,
      false
    );

    if (response.success && response.token) {
      apiClient.setToken(response.token, response.refreshToken);
    }

    return response;
  },

  /** Emails a reset link. Always resolves — the response is identical whether or not the account exists. */
  async requestPasswordReset(email: string, locale: string): Promise<{ success: boolean }> {
    return apiClient.post('/auth/request-password-reset', { email, locale }, false, false);
  },

  /** Sets a new password from a reset-link token. Throws ApiError (code `invalid_or_expired`) on a bad token. */
  async resetPassword(token: string, password: string): Promise<{ success: boolean; code?: string }> {
    return apiClient.post('/auth/reset-password', { token, password }, false, false);
  },

  /** Emails a confirmation link that finishes account deletion. Requires an active session. */
  async requestAccountDeletion(locale: string): Promise<{ success: boolean }> {
    return apiClient.post('/auth/request-account-deletion', { locale }, true, true);
  },

  /** Deletes the account named by a delete-confirmation token. */
  async confirmAccountDeletion(token: string): Promise<{ success: boolean; code?: string }> {
    return apiClient.post('/auth/confirm-delete', { token }, false, false);
  },

  /** Deletes an account from the "I didn't sign up" link in the welcome email. */
  async revokeSignup(token: string): Promise<{ success: boolean; code?: string }> {
    return apiClient.post('/auth/revoke-signup', { token }, false, false);
  },

  logout(): void {
    apiClient.clearToken();
  },

  isAuthenticated(): boolean {
    if (typeof window === 'undefined') return false;
    return !!localStorage.getItem('token');
  },
};
