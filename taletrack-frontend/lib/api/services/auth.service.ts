import { apiClient } from '../client';
import type {
  LoginRequest,
  LoginResponse,
  GoogleNeedsUsernameResponse,
  RegisterRequest,
  RegisterResponse,
} from '../../types';

export const authService = {
  async login(email: string, password: string): Promise<LoginResponse> {
    const response = await apiClient.post<LoginResponse>(
      '/login',
      { email, password } as LoginRequest,
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
    );
  },

  async googleLogin(
    idToken: string,
    locale: string,
  ): Promise<LoginResponse | GoogleNeedsUsernameResponse> {
    const response = await apiClient.post<LoginResponse | GoogleNeedsUsernameResponse>(
      '/auth/google',
      { idToken, locale },
    );

    if ('token' in response && response.success && response.token) {
      apiClient.setToken(response.token, response.refreshToken);
    }

    return response;
  },

  /** Finishes a Google sign-up once the user has picked a username. */
  async completeGoogleSignup(
    pendingToken: string,
    username: string,
    locale: string,
  ): Promise<LoginResponse> {
    const response = await apiClient.post<LoginResponse>('/auth/google/complete', {
      pendingToken,
      username,
      locale,
    });

    if (response.success && response.token) {
      apiClient.setToken(response.token, response.refreshToken);
    }

    return response;
  },

  /** Emails a reset link. Always resolves — the response is identical whether or not the account exists. */
  async requestPasswordReset(email: string, locale: string): Promise<{ success: boolean }> {
    return apiClient.post('/auth/request-password-reset', { email, locale });
  },

  /** Sets a new password from a reset-link token. Throws ApiError (code `invalid_or_expired`) on a bad token. */
  async resetPassword(token: string, password: string): Promise<{ success: boolean; code?: string }> {
    return apiClient.post('/auth/reset-password', { token, password });
  },

  /** Emails a confirmation link that finishes account deletion. Requires an active session. */
  async requestAccountDeletion(locale: string): Promise<{ success: boolean }> {
    return apiClient.post('/auth/request-account-deletion', { locale }, true);
  },

  /** Deletes the account named by a delete-confirmation token. */
  async confirmAccountDeletion(token: string): Promise<{ success: boolean; code?: string }> {
    return apiClient.post('/auth/confirm-delete', { token });
  },

  /** Deletes an account from the "I didn't sign up" link in the welcome email. */
  async revokeSignup(token: string): Promise<{ success: boolean; code?: string }> {
    return apiClient.post('/auth/revoke-signup', { token });
  },

  logout(): void {
    apiClient.clearToken();
  },

  isAuthenticated(): boolean {
    if (typeof window === 'undefined') return false;
    return !!localStorage.getItem('token');
  },
};
