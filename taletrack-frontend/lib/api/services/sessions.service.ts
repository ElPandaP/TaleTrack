import { apiClient } from '../client';

/** An active session: one signed-in device, backed by a refresh token. */
export interface Session {
  /** Session id, used to revoke it. */
  id: string;
  /** Device label sent at sign-in (e.g. `Web`, the extension, KOReader). */
  device: string;
  /** ISO date the session was created. */
  createdAt: string;
  /** ISO date the session last refreshed its token. */
  lastUsedAt: string;
  /** ISO date the refresh token expires. */
  expiresAt: string;
}

/** Calls for listing and revoking sessions, and for authorizing the browser extension. */
export const sessionsService = {
  /** Lists the caller's active sessions. */
  async list(): Promise<Session[]> {
    const res = await apiClient.get<{ success: boolean; data: Session[] }>(
      '/auth/sessions',
      true,
    );
    return res.data ?? [];
  },

  /** Revokes a session, signing that device out. */
  async revoke(id: string): Promise<void> {
    await apiClient.delete(`/auth/sessions/${id}`, true);
  },

  /** Trades the current web session for a new access and refresh token pair meant for the extension. */
  async extensionGrant(device?: string): Promise<{ token: string; refreshToken: string; expiresIn: number }> {
    return apiClient.post('/auth/extension-grant', device ? { device } : {}, true);
  },

  /** Revokes a pair obtained from {@link sessionsService.extensionGrant} that never reached the extension. */
  discardExtensionGrant(refreshToken: string): void {
    void apiClient.revokeRefreshToken(refreshToken);
  },
};
