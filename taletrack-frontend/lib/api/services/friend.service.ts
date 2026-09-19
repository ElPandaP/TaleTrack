import { apiClient } from '../client';
import type { UserSearchResponse } from '../../types';

export const friendService = {
  async searchByUsername(username: string): Promise<UserSearchResponse> {
    return apiClient.get<UserSearchResponse>(
      `/users/search?username=${encodeURIComponent(username)}`,
      true,
    );
  },

  async sendRequest(userId: string): Promise<{ success: boolean; message?: string }> {
    return apiClient.post('/friends/requests', { userId }, true);
  },

  async accept(requestId: string): Promise<{ success: boolean }> {
    return apiClient.put(`/friends/requests/${requestId}`, undefined, true);
  },

  async decline(requestId: string): Promise<{ success: boolean }> {
    return apiClient.delete(`/friends/requests/${requestId}`, true);
  },

  async remove(userId: string): Promise<{ success: boolean }> {
    return apiClient.delete(`/friends/${userId}`, true);
  },
};
