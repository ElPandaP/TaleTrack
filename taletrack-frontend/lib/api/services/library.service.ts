import { apiClient } from '../client';
import type { GetLibraryResponse, GetStatsResponse } from '../../types';

export interface LibraryQuery {
  type?: string;
  status?: 'in_progress' | 'finished';
  sort?: 'recent' | 'rating';
  year?: number;
  limit?: number;
}

export const libraryService = {
  /** The current user's library, one row per media. Backend policy: JWT only. */
  async getLibrary(query: LibraryQuery = {}): Promise<GetLibraryResponse> {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null) params.set(key, String(value));
    }
    const qs = params.toString();
    return apiClient.get<GetLibraryResponse>(`/library${qs ? `?${qs}` : ''}`, true);
  },

  async getPendingReviews(): Promise<GetLibraryResponse> {
    return apiClient.get<GetLibraryResponse>('/reviews/pending', true);
  },

  async getStats(year?: number): Promise<GetStatsResponse> {
    return apiClient.get<GetStatsResponse>(`/stats${year ? `?year=${year}` : ''}`, true);
  },
};
