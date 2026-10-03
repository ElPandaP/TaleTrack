/**
 * Data fetchers for Server Components.
 *
 * @remarks
 * They call the backend directly at `INTERNAL_API_URL` and authenticate with the `tt-token`
 * cookie, since `localStorage` is not available on the server. Every function throws on a
 * non-2xx response; pages catch the error and fall back to an empty state.
 *
 * @module
 */
import { cache } from 'react';
import { cookies } from 'next/headers';
import { ACCESS_TOKEN_COOKIE } from '@/lib/auth-storage';
import type {
  GetStatsResponse,
  GetLibraryResponse,
  GetReviewsResponse,
  GetMediaDetailResponse,
  FriendsResponse,
  ActivityResponse,
  UserProfileResponse,
  PublicProfileResponse,
} from '../types';

const SERVER_BASE_URL = process.env.INTERNAL_API_URL ?? 'http://localhost:8080/api';

/**
 * GETs `endpoint` with the session cookie as a bearer token and parses the JSON body.
 *
 * @throws An `Error` carrying the status code when the response is not OK.
 */
async function serverFetch<T>(endpoint: string): Promise<T> {
  const cookieStore = await cookies();
  const token = cookieStore.get(ACCESS_TOKEN_COOKIE)?.value;

  const headers: HeadersInit = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${SERVER_BASE_URL}${endpoint}`, {
    headers,
    cache: 'no-store', // user-specific data: never cache across requests
  });

  if (!res.ok) throw new Error(`API ${res.status}`);
  return res.json() as Promise<T>;
}

/** Fetches the signed-in user's stats for the current year (totals by type and by month). */
export async function getStats(): Promise<GetStatsResponse> {
  return serverFetch<GetStatsResponse>('/stats');
}

/** Optional filters for {@link getLibrary}; each one set is sent as a query parameter. */
export interface LibraryQuery {
  /** Media type (`Book`, `Movie` or `Series`). */
  type?: string;
  /** Only items still in progress, or only finished ones. */
  status?: 'in_progress' | 'finished';
  /** Maximum number of items to return. */
  limit?: number;
}

/**
 * Fetches the signed-in user's library: one entry per tracked media, with progress and rating.
 *
 * @param query - Filters; with none, the whole library is returned.
 */
export async function getLibrary(query: LibraryQuery = {}): Promise<GetLibraryResponse> {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null) params.set(key, String(value));
  }
  const qs = params.toString();
  return serverFetch<GetLibraryResponse>(`/library${qs ? `?${qs}` : ''}`);
}

/** Fetches the finished media the signed-in user has not reviewed yet. */
export async function getPendingReviews(): Promise<GetLibraryResponse> {
  return serverFetch<GetLibraryResponse>('/reviews/pending');
}

/** Fetches every review written by the signed-in user. */
export async function getReviews(): Promise<GetReviewsResponse> {
  return serverFetch<GetReviewsResponse>('/reviews');
}

/**
 * Fetches a media's detail page data: metadata, the user's own status and everyone's reviews.
 *
 * @param id - The media id.
 */
export async function getMediaDetail(id: string): Promise<GetMediaDetailResponse> {
  return serverFetch<GetMediaDetailResponse>(`/media/${id}`);
}

/** Fetches the signed-in user's friends plus incoming and outgoing friend requests. */
export async function getFriends(): Promise<FriendsResponse> {
  return serverFetch<FriendsResponse>('/friends');
}

/**
 * Fetches the activity feed (started, finished, reviewed) of the user and their friends.
 *
 * @param scope - Whose events to include.
 * @param limit - Maximum number of events.
 */
export async function getActivity(
  scope: 'all' | 'mine' | 'friends' = 'all',
  limit = 200,
): Promise<ActivityResponse> {
  return serverFetch<ActivityResponse>(`/activity?scope=${scope}&limit=${limit}`);
}

/**
 * Fetches one user's activity, as far as their privacy settings allow the viewer to see it.
 *
 * @param userId - The user whose events to fetch.
 * @param limit - Maximum number of events.
 */
export async function getUserActivity(userId: string, limit = 100): Promise<ActivityResponse> {
  return serverFetch<ActivityResponse>(`/activity?userId=${userId}&limit=${limit}`);
}

/**
 * Fetches a user's public profile: name, avatar, library counts and relationship to the viewer.
 *
 * @param id - The user id.
 */
export async function getUserProfile(id: string): Promise<PublicProfileResponse> {
  return serverFetch<PublicProfileResponse>(`/users/${id}`);
}

/**
 * Fetches the signed-in user's own profile, including email and privacy settings.
 *
 * @remarks
 * Wrapped in React's `cache`, so the root layout and a page calling it in the same request
 * share a single backend call.
 */
export const getMe = cache((): Promise<UserProfileResponse> =>
  serverFetch<UserProfileResponse>('/users/me'),
);
