import { apiClient } from '../client';

export const trackingService = {
  /** Removes all tracking for a media, taking it out of the user's library. Backend policy: JWT only. */
  async deleteTracking(mediaId: string): Promise<{ success: boolean; message: string }> {
    return apiClient.delete<{ success: boolean; message: string }>(`/tracking/${mediaId}`, true);
  },

  /** Overrides the progress of the most recent tracking event for a media. Backend policy: JWT only. */
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

  /** Series only — logs the furthest episode reached; earlier episodes are assumed watched. */
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
