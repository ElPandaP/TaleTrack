import { apiClient } from '../client';
import type { FeedPrivacy } from '../../types';

/** Fields of a profile edit; omitted fields are left unchanged. */
export interface ProfileUpdate {
  /** New username. */
  username?: string;
  /** Feed privacy flags to change. */
  privacy?: Partial<FeedPrivacy>;
}

/** Calls that edit the caller's own profile and avatar. */
export const userService = {
  /**
   * Updates the profile.
   *
   * @returns The result, with a freshly issued access token (the token embeds the username).
   */
  async updateProfile(
    id: string,
    patch: ProfileUpdate,
  ): Promise<{ success: boolean; message?: string; token?: string }> {
    return apiClient.put(`/users/${id}`, patch, true);
  },

  /** Uploads a profile photo; the backend crops it to a 256x256 WebP and returns its URL. */
  async uploadAvatar(file: File): Promise<{ success: boolean; avatarUrl: string }> {
    const form = new FormData();
    form.append('file', file);
    return apiClient.putForm('/users/me/avatar', form, true);
  },

  /** Deletes the profile photo. */
  async removeAvatar(): Promise<{ success: boolean }> {
    return apiClient.delete('/users/me/avatar', true);
  },
};
