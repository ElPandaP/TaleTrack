import { apiClient } from '../client';
import type { AddMediaRequest, AddMediaResponse, MediaType } from '../../types';

export const mediaService = {
  async addMedia(title: string, type: MediaType, length?: number): Promise<AddMediaResponse> {
    return apiClient.post<AddMediaResponse>(
      '/media',
      { title, type, length } as AddMediaRequest,
      true,
      false
    );
  },
};
