import { apiClient } from '../client';
import type { UserProfileResponse, FeedPrivacy } from '../../types';

export interface ProfileUpdate {
  username?: string;
  email?: string;
  avatarUrl?: string;
  privacy?: Partial<FeedPrivacy>;
}

export const userService = {
  async getMe(): Promise<UserProfileResponse> {
    return apiClient.get<UserProfileResponse>('/user/me', true, false);
  },

  /** EditUser is JWT + internal key on the backend. */
  async updateProfile(id: number, patch: ProfileUpdate): Promise<{ success: boolean; message?: string }> {
    return apiClient.put(`/user/${id}`, patch, true, true);
  },

  /** Uploads a profile photo; the backend crops it to a 256×256 WebP and returns its URL. */
  async uploadAvatar(file: File): Promise<{ success: boolean; avatarUrl: string }> {
    const form = new FormData();
    form.append('file', file);
    return apiClient.postForm('/user/avatar', form, true, true);
  },

  async removeAvatar(): Promise<{ success: boolean }> {
    return apiClient.delete('/user/avatar', true, true);
  },
};
