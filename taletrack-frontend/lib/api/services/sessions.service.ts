import { apiClient } from '../client';

export interface Session {
  id: number;
  device: string;
  createdAt: string;
  lastUsedAt: string;
  expiresAt: string;
}

export const sessionsService = {
  async list(): Promise<Session[]> {
    const res = await apiClient.get<{ success: boolean; data: Session[] }>(
      '/auth/sessions',
      true,
    );
    return res.data ?? [];
  },

  async revoke(id: number): Promise<void> {
    await apiClient.delete(`/auth/sessions/${id}`, true);
  },

  /** Trades the current web JWT for an extension access + refresh token pair. */
  async extensionGrant(): Promise<{ token: string; refreshToken: string; expiresIn: number }> {
    return apiClient.post('/auth/extension-grant', {}, true);
  },
};
