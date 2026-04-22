import { apiClient } from '../client';
import type { GetTrackingEventsResponse, GetUserBooksResponse } from '../../types';

export const trackingService = {
  async addTrackingEvent(
    title: string,
    type: string,
    progress: number,
    length?: number
  ): Promise<{ success: boolean; message: string }> {
    const params = new URLSearchParams();
    params.append('title', title);
    params.append('type', type);
    params.append('progress', progress.toString());
    if (length) params.append('length', length.toString());
    
    const queryString = params.toString();
    return apiClient.post<{ success: boolean; message: string }>(
      `/tracking?${queryString}`,
      {},
      true,
      false
    );
  },

  async getTrackingEvents(
    type?: string,
    limit?: number,
    orderBy?: string
  ): Promise<GetTrackingEventsResponse> {
    const params = new URLSearchParams();
    
    if (type) params.append('type', type);
    if (limit) params.append('limit', limit.toString());
    if (orderBy) params.append('orderBy', orderBy);
    
    const queryString = params.toString();
    const endpoint = queryString ? `/tracking?${queryString}` : '/tracking';
    
    return apiClient.get<GetTrackingEventsResponse>(
      endpoint,
      true,
      true
    );
  },

  async getBooks(): Promise<GetUserBooksResponse> {
    return apiClient.get<GetUserBooksResponse>('/books', true, false);
  },
};
