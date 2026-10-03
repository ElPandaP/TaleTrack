import { apiClient } from '../client';
import type { UserSearchResponse } from '../../types';

/** Friend calls: user search, friend requests and removing friends. */
export const friendService = {
  /** Looks up a user by exact username and returns their relationship to the caller. */
  async searchByUsername(username: string): Promise<UserSearchResponse> {
    return apiClient.get<UserSearchResponse>(
      `/users/search?username=${encodeURIComponent(username)}`,
      true,
    );
  },

  /** Sends a friend request to a user. */
  async sendRequest(userId: string): Promise<{ success: boolean; message?: string }> {
    return apiClient.post('/friends/requests', { userId }, true);
  },

  /** Accepts an incoming friend request. */
  async accept(requestId: string): Promise<{ success: boolean }> {
    return apiClient.put(`/friends/requests/${requestId}`, undefined, true);
  },

  /** Declines an incoming friend request. */
  async decline(requestId: string): Promise<{ success: boolean }> {
    return apiClient.delete(`/friends/requests/${requestId}`, true);
  },

  /** Removes a user from the caller's friends. */
  async remove(userId: string): Promise<{ success: boolean }> {
    return apiClient.delete(`/friends/${userId}`, true);
  },
};
