import { apiClient } from '../client';
import type { FeedPrivacy } from '../../types';

export interface ProfileUpdate {
  username?: string;
  privacy?: Partial<FeedPrivacy>;
}

export const userService = {
  async updateProfile(
    id: string,
    patch: ProfileUpdate,
  ): Promise<{ success: boolean; message?: string; token?: string }> {
    return apiClient.put(`/users/${id}`, patch, true);
  },

  /** Uploads a profile photo; the backend crops it to a 256×256 WebP and returns its URL. */
  async uploadAvatar(file: File): Promise<{ success: boolean; avatarUrl: string }> {
    const form = new FormData();
    form.append('file', file);
    return apiClient.putForm('/users/me/avatar', form, true);
  },

  async removeAvatar(): Promise<{ success: boolean }> {
    return apiClient.delete('/users/me/avatar', true);
  },
};
