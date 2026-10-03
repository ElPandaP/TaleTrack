import { getReviews } from '@/lib/api/server';
import type { ReviewItem } from '@/lib/types';
import ReviewsClient from './ReviewsClient';

/** `/reviews`: every review the user has written. */
export default async function ReviewsPage() {
  let reviews: ReviewItem[] = [];
  try {
    const res = await getReviews();
    reviews = res.data ?? [];
  } catch {
    // ReviewsClient shows the empty state.
  }
  return <ReviewsClient reviews={reviews} />;
}
