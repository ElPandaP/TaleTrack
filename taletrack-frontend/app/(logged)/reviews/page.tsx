import { getReviews } from '@/lib/api/server';
import type { ReviewItem } from '@/lib/types';
import ReviewsClient from './ReviewsClient';

export default async function ReviewsPage() {
  let reviews: ReviewItem[] = [];
  try {
    const res = await getReviews();
    reviews = res.data ?? [];
  } catch {
    // empty state handled in the client
  }
  return <ReviewsClient reviews={reviews} />;
}
