// User types
export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  success: boolean;
  message: string;
  token: string;
  refreshToken?: string;
  expiresIn?: number;
}

export interface RegisterRequest {
  email: string;
  username: string;
  password: string;
}

export interface RegisterResponse {
  success: boolean;
  message: string;
}

// Review types
export interface AddReviewResponse {
  success: boolean;
  message: string;
}

// Home / Library / Stats — the backend uses these three type slugs for Media.type.
export type LibraryType = 'Book' | 'Movie' | 'Series';

export interface LibraryItem {
  mediaId: number;
  title: string;
  type: LibraryType;
  author?: string | null;
  posterUrl?: string | null;
  length: number;
  isbn?: string | null;
  progress?: number | null;
  lastEventDate: string;
  myRating?: number | null;
  myReviewId?: number | null;
}

export interface GetLibraryResponse {
  success: boolean;
  count: number;
  /** Total matching items before any limit — for "50 · see all" style counts. */
  total?: number;
  data: LibraryItem[];
}

export interface YearlyStats {
  year: number;
  total: number;
  byType: { book: number; movie: number; series: number };
  byMonth: number[]; // length 12, Jan → Dec
  reviewCount: number;
}

export interface GetStatsResponse {
  success: boolean;
  data: YearlyStats;
}

export interface ReviewItem {
  id: number;
  mediaId: number;
  rating: number;
  comment?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  media?: {
    id: number;
    title: string;
    type: LibraryType;
    posterUrl?: string | null;
    author?: string | null;
  };
}

export interface GetReviewsResponse {
  success: boolean;
  count: number;
  data: ReviewItem[];
}

export interface EditReviewRequest {
  rating: number;
  comment?: string;
}

export interface MediaDetailReview {
  id: number;
  rating: number;
  comment?: string | null;
  createdAt: string;
  username?: string | null;
  mine: boolean;
}

export interface MediaDetail {
  id: number;
  title: string;
  type: LibraryType;
  author?: string | null;
  posterUrl?: string | null;
  length: number;
  isbn?: string | null;
  description?: string | null;
  avgRating?: number | null;
  reviewCount: number;
  myProgress?: number | null;
  myLastEventDate?: string | null;
  myReviewId?: number | null;
  myRating?: number | null;
  myComment?: string | null;
  reviews: MediaDetailReview[];
}

export interface GetMediaDetailResponse {
  success: boolean;
  data: MediaDetail;
}

// Friends / Activity / Profile
export interface Friend {
  userId: number;
  username: string;
  avatarUrl?: string | null;
}

export interface FriendRequest {
  requestId: number;
  userId: number;
  username: string;
  avatarUrl?: string | null;
  createdAt: string;
}

export interface FriendsResponse {
  success: boolean;
  friends: Friend[];
  incoming: FriendRequest[];
  outgoing: FriendRequest[];
}

export type UserRelationship = 'none' | 'self' | 'friends' | 'incoming' | 'outgoing';

export interface UserSearchResponse {
  success: boolean;
  user: { userId: number; username: string; avatarUrl?: string | null } | null;
  relationship?: UserRelationship;
}

export type ActivityKind = 'started' | 'finished' | 'reviewed';

export interface ActivityItem {
  id: string;
  userId: number;
  username: string;
  avatarUrl?: string | null;
  kind: ActivityKind;
  date: string;
  mediaId: number;
  mediaTitle: string;
  mediaType: LibraryType;
  mediaPosterUrl?: string | null;
  rating?: number | null;
  comment?: string | null;
}

export interface ActivityResponse {
  success: boolean;
  count: number;
  data: ActivityItem[];
}

export interface FeedPrivacy {
  bookProgress: boolean;
  bookReviews: boolean;
  movieProgress: boolean;
  movieReviews: boolean;
  seriesProgress: boolean;
  seriesReviews: boolean;
}

export interface UserProfile {
  id: number;
  username: string;
  email: string;
  avatarUrl?: string | null;
  createdAt: string;
  privacy: FeedPrivacy;
}

export interface UserProfileResponse {
  success: boolean;
  data: UserProfile;
}

export interface PublicProfile {
  id: number;
  username: string;
  avatarUrl?: string | null;
  createdAt: string;
  relationship: UserRelationship;
  counts: { book: number; movie: number; series: number; total: number };
}

export interface PublicProfileResponse {
  success: boolean;
  data: PublicProfile;
}
