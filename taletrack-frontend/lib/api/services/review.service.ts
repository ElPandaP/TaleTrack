import { apiClient } from '../client';
import type {
  AddReviewResponse,
  EditReviewRequest,
} from '../../types';

export const reviewService = {
  /** Create a review. */
  async addReview(mediaId: string, rating: number, comment?: string): Promise<AddReviewResponse> {
    return apiClient.post<AddReviewResponse>(
      '/reviews',
      { mediaId, rating, comment },
      true,
    );
  },

  async editReview(id: string, rating: number, comment?: string): Promise<AddReviewResponse> {
    return apiClient.put<AddReviewResponse>(
      `/reviews/${id}`,
      { rating, comment } as EditReviewRequest,
      true,
    );
  },

  async deleteReview(id: string): Promise<{ success: boolean; message: string }> {
    return apiClient.delete<{ success: boolean; message: string }>(
      `/reviews/${id}`,
      true,
    );
  },
};
