import { apiClient } from '../client';
import type {
  AddReviewResponse,
  EditReviewRequest,
} from '../../types';

/** Review calls. Ratings use the backend's 1-10 scale. */
export const reviewService = {
  /** Creates a review for a media. */
  async addReview(mediaId: string, rating: number, comment?: string): Promise<AddReviewResponse> {
    return apiClient.post<AddReviewResponse>(
      '/reviews',
      { mediaId, rating, comment },
      true,
    );
  },

  /** Updates the rating and comment of an existing review. */
  async editReview(id: string, rating: number, comment?: string): Promise<AddReviewResponse> {
    return apiClient.put<AddReviewResponse>(
      `/reviews/${id}`,
      { rating, comment } as EditReviewRequest,
      true,
    );
  },

  /** Deletes one of the caller's reviews. */
  async deleteReview(id: string): Promise<{ success: boolean; message: string }> {
    return apiClient.delete<{ success: boolean; message: string }>(
      `/reviews/${id}`,
      true,
    );
  },
};
