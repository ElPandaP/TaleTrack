/**
 * Shapes of the backend's request and response bodies, as the frontend uses them.
 *
 * @module
 */

// --- Auth ---

/** Body of `POST /login`. */
export interface LoginRequest {
  email: string;
  password: string;
}

/** Successful sign-in from any method: the new token pair. */
export interface LoginResponse {
  success: boolean;
  message: string;
  /** Access token (JWT). */
  token: string;
  /** Refresh token for this device's session. */
  refreshToken?: string;
  /** Access token lifetime in seconds. */
  expiresIn?: number;
  /** Google login only: an existing email/password account was just linked to this Google identity. */
  linkedExistingAccount?: boolean;
}

/**
 * Returned by `/auth/google` instead of a {@link LoginResponse} on a first Google sign-up: no
 * account exists yet, and the user must pick a username before one is created.
 */
export interface GoogleNeedsUsernameResponse {
  success: true;
  needsUsername: true;
  /** Short-lived token that identifies the pending sign-up when completing it. */
  pendingToken: string;
  email: string;
  /** Username proposed from the Google profile. */
  suggestedUsername: string;
}

/** Body of `POST /register`. */
export interface RegisterRequest {
  email: string;
  username: string;
  password: string;
  locale: string;
}

/** Result of a registration. */
export interface RegisterResponse {
  success: boolean;
  message: string;
}

// --- Reviews ---

/** Result of creating or editing a review. */
export interface AddReviewResponse {
  success: boolean;
  message: string;
}

// --- Library and stats ---

/** The three media types, as the backend names them. */
export type LibraryType = 'Book' | 'Movie' | 'Series';

/** One media in the user's library, with the user's progress and rating. */
export interface LibraryItem {
  mediaId: string;
  titleEN?: string | null;
  titleES?: string | null;
  type: LibraryType;
  author?: string | null;
  posterUrl?: string | null;
  /** Pages for a book, minutes for a movie, minutes of one episode for a series. */
  length: number;
  isbn?: string | null;
  /** Progress percentage (0-100); 100 means finished. */
  progress?: number | null;
  /** ISO date of the latest tracking event. */
  lastEventDate: string;
  /** The user's rating on the 1-10 scale, if reviewed. */
  myRating?: number | null;
  myReviewId?: string | null;
  /** Series only: the latest episode reported, and the episode count per season (index 0 = season 1). */
  season?: number | null;
  episode?: number | null;
  seasonEpisodeCounts?: number[] | null;
}

/** Response of `GET /library`. */
export interface GetLibraryResponse {
  success: boolean;
  /** Number of items returned. */
  count: number;
  /** Number of matching items before the limit, for "see all (N)" style counts. */
  total?: number;
  data: LibraryItem[];
}

/** Summary of one year of activity. */
export interface YearlyStats {
  year: number;
  /** Distinct media with activity in the year. */
  total: number;
  byType: { book: number; movie: number; series: number };
  /** Media per month, January first (12 entries). */
  byMonth: number[];
  /** Reviews written in the year. */
  reviewCount: number;
}

/** Response of `GET /stats`. */
export interface GetStatsResponse {
  success: boolean;
  data: YearlyStats;
}

/** One of the user's reviews, with a summary of the media it belongs to. */
export interface ReviewItem {
  id: string;
  mediaId: string;
  /** Rating on the 1-10 scale. */
  rating: number;
  comment?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  media?: {
    id: string;
    titleEN?: string | null;
    titleES?: string | null;
    type: LibraryType;
    posterUrl?: string | null;
    author?: string | null;
  };
}

/** Response of `GET /reviews`. */
export interface GetReviewsResponse {
  success: boolean;
  count: number;
  data: ReviewItem[];
}

/** Body of `PUT /reviews/{id}`. */
export interface EditReviewRequest {
  rating: number;
  comment?: string;
}

// --- Media detail ---

/** A review shown on a media's detail page. */
export interface MediaDetailReview {
  id: string;
  /** Rating on the 1-10 scale. */
  rating: number;
  comment?: string | null;
  createdAt: string;
  username?: string | null;
  /** True for the viewer's own review. */
  mine: boolean;
}

/** Everything the media detail page shows: metadata, the viewer's own status and all reviews. */
export interface MediaDetail {
  id: string;
  titleEN?: string | null;
  titleES?: string | null;
  type: LibraryType;
  author?: string | null;
  posterUrl?: string | null;
  /** Pages for a book, minutes for a movie, minutes of one episode for a series. */
  length: number;
  isbn?: string | null;
  description?: string | null;
  /** Average rating on the 1-10 scale, if anyone reviewed it. */
  avgRating?: number | null;
  reviewCount: number;
  /** The viewer's progress (0-100), or null if they do not track it. */
  myProgress?: number | null;
  myLastEventDate?: string | null;
  /** Series only: the latest episode reported, and the episode count per season (index 0 = season 1). */
  mySeason?: number | null;
  myEpisode?: number | null;
  seasonEpisodeCounts?: number[] | null;
  myReviewId?: string | null;
  myRating?: number | null;
  myComment?: string | null;
  reviews: MediaDetailReview[];
}

/** Response of `GET /media/{id}`. */
export interface GetMediaDetailResponse {
  success: boolean;
  data: MediaDetail;
}

// --- Friends, activity and profile ---

/** A friend in the user's friend list. */
export interface Friend {
  userId: string;
  username: string;
  avatarUrl?: string | null;
}

/** A pending friend request; `userId` is the other user (sender or receiver). */
export interface FriendRequest {
  requestId: string;
  userId: string;
  username: string;
  avatarUrl?: string | null;
  createdAt: string;
}

/** Response of `GET /friends`. */
export interface FriendsResponse {
  success: boolean;
  friends: Friend[];
  incoming: FriendRequest[];
  outgoing: FriendRequest[];
}

/** How another user relates to the viewer: none, the viewer themself, friends, or a pending request either way. */
export type UserRelationship = 'none' | 'self' | 'friends' | 'incoming' | 'outgoing';

/** Response of a username search: the user found (or `null`) and their relationship to the caller. */
export interface UserSearchResponse {
  success: boolean;
  user: { userId: string; username: string; avatarUrl?: string | null } | null;
  relationship?: UserRelationship;
}

/** What an activity event records. */
export type ActivityKind = 'started' | 'finished' | 'reviewed';

/** One event in an activity feed: who did what with which media, and when. */
export interface ActivityItem {
  id: string;
  userId: string;
  username: string;
  avatarUrl?: string | null;
  kind: ActivityKind;
  date: string;
  mediaId: string;
  mediaTitleEN?: string | null;
  mediaTitleES?: string | null;
  mediaType: LibraryType;
  mediaPosterUrl?: string | null;
  /** Reviews only: rating on the 1-10 scale. */
  rating?: number | null;
  /** Reviews only: the review text. */
  comment?: string | null;
}

/** Response of `GET /activity`. */
export interface ActivityResponse {
  success: boolean;
  count: number;
  data: ActivityItem[];
}

/** Which of the user's events friends can see in their feed, per media type. */
export interface FeedPrivacy {
  bookProgress: boolean;
  bookReviews: boolean;
  movieProgress: boolean;
  movieReviews: boolean;
  seriesProgress: boolean;
  seriesReviews: boolean;
}

/** The signed-in user's own profile. */
export interface UserProfile {
  id: string;
  username: string;
  email: string;
  avatarUrl?: string | null;
  createdAt: string;
  privacy: FeedPrivacy;
}

/** Response of `GET /users/me`. */
export interface UserProfileResponse {
  success: boolean;
  data: UserProfile;
}

/** Another user's profile as the viewer sees it. */
export interface PublicProfile {
  id: string;
  username: string;
  avatarUrl?: string | null;
  createdAt: string;
  relationship: UserRelationship;
  /** Number of tracked media per type. */
  counts: { book: number; movie: number; series: number; total: number };
}

/** Response of `GET /users/{id}`. */
export interface PublicProfileResponse {
  success: boolean;
  data: PublicProfile;
}
