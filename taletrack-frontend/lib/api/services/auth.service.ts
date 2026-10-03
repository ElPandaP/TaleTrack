import { apiClient } from '../client';
import type {
  LoginRequest,
  LoginResponse,
  GoogleNeedsUsernameResponse,
  RegisterRequest,
  RegisterResponse,
} from '../../types';

/**
 * Authentication calls: sign-in (password, Google, email code), registration, password reset,
 * account deletion and logout. Methods that sign the user in store the returned tokens through
 * `apiClient`; the caller still has to notify the auth context.
 */
export const authService = {
  /** Signs in with email and password and stores the tokens on success. */
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

  /**
   * Creates an account. It does not sign in; callers log in afterwards.
   *
   * @param locale - UI locale, used for the welcome email.
   */
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

  /**
   * Signs in with a Google ID token and stores the tokens on success.
   *
   * @returns A normal login response, or a "needs username" response when this Google account
   * has no TaleTrack account yet (finish it with `completeGoogleSignup`).
   */
  async googleLogin(idToken: string): Promise<LoginResponse | GoogleNeedsUsernameResponse> {
    const response = await apiClient.post<LoginResponse | GoogleNeedsUsernameResponse>(
      '/auth/google',
      { idToken },
    );

    if ('token' in response && response.success && response.token) {
      apiClient.setToken(response.token, response.refreshToken);
    }

    return response;
  },

  /** Finishes a Google sign-up once the user has picked a username, and stores the tokens. */
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

  /** Emails a password reset link. The response is the same whether or not the account exists. */
  async requestPasswordReset(email: string, locale: string): Promise<{ success: boolean }> {
    return apiClient.post('/auth/request-password-reset', { email, locale });
  },

  /** Emails a 6-digit login code. The response is the same whether or not the account exists. */
  async requestLoginCode(email: string, locale: string): Promise<{ success: boolean }> {
    return apiClient.post('/auth/request-code', { email, locale });
  },

  /** Verifies a login code, signs in and stores the tokens. Throws an `ApiError` with status 401 on a wrong or expired code. */
  async verifyLoginCode(email: string, code: string): Promise<LoginResponse> {
    const response = await apiClient.post<LoginResponse>('/auth/verify-code', {
      email,
      code,
      device: 'Web',
    });

    if (response.success && response.token) {
      apiClient.setToken(response.token, response.refreshToken);
    }

    return response;
  },

  /** Sets a new password from a reset-link token. Throws an `ApiError` with code `invalid_or_expired` on a bad token. */
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

  /** Revokes this device's session on the server (best effort) and drops the local tokens. */
  logout(): void {
    void apiClient.revokeRefreshToken();
    apiClient.clearToken();
  },
};
