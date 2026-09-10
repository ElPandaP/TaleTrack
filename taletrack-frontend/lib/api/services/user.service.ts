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

};
