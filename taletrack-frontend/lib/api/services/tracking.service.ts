import { apiClient } from '../client';

/** Calls that change the caller's progress on a media. */
export const trackingService = {
  /** Removes all tracking for a media, taking it out of the user's library. */
  async deleteTracking(mediaId: string): Promise<{ success: boolean; message: string }> {
    return apiClient.delete<{ success: boolean; message: string }>(`/tracking/${mediaId}`, true);
  },

  /** Sets the caller's progress (0-100) on a media they already track. */
  async editProgress(
    mediaId: string,
    progress: number
  ): Promise<{ success: boolean; message: string; data: { progress: number } }> {
    return apiClient.put<{ success: boolean; message: string; data: { progress: number } }>(
      `/tracking/${mediaId}`,
      { progress },
      true,
    );
  },

  /** Series only: sets the episode reached; earlier episodes count as watched. */
  async editSeriesEpisode(
    mediaId: string,
    season: number,
    episode: number,
  ): Promise<{ success: boolean; message: string; data: { progress: number } }> {
    return apiClient.put<{ success: boolean; message: string; data: { progress: number } }>(
      `/tracking/${mediaId}`,
      { season, episode },
      true,
    );
  },
};
