import { cache } from 'react';
import { cookies } from 'next/headers';
import { API_CONFIG } from './config';
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

async function serverFetch<T>(
  endpoint: string,
  requireApiKey = false,
): Promise<T> {
  const cookieStore = await cookies();
  const token = cookieStore.get('tt-token')?.value;

  const headers: HeadersInit = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  if (requireApiKey && API_CONFIG.internalApiKey) {
    headers['X-Internal-Api-Key'] = API_CONFIG.internalApiKey;
  }

  const res = await fetch(`${SERVER_BASE_URL}${endpoint}`, {
    headers,
    cache: 'no-store', // user-specific data — never cache across requests
  });

  if (!res.ok) throw new Error(`API ${res.status}`);
  return res.json() as Promise<T>;
}

export async function getStats(year?: number): Promise<GetStatsResponse> {
  const q = year ? `?year=${year}` : '';
  return serverFetch<GetStatsResponse>(`/stats${q}`);
}

export interface LibraryQuery {
  type?: string;
  status?: 'in_progress' | 'finished';
  sort?: 'recent' | 'rating';
  year?: number;
  limit?: number;
}

export async function getLibrary(query: LibraryQuery = {}): Promise<GetLibraryResponse> {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null) params.set(key, String(value));
  }
  const qs = params.toString();
  return serverFetch<GetLibraryResponse>(`/library${qs ? `?${qs}` : ''}`);
}

export async function getPendingReviews(): Promise<GetLibraryResponse> {
  return serverFetch<GetLibraryResponse>('/reviews/pending');
}

export async function getReviews(): Promise<GetReviewsResponse> {
  return serverFetch<GetReviewsResponse>('/reviews');
}

export async function getMediaDetail(id: number | string): Promise<GetMediaDetailResponse> {
  return serverFetch<GetMediaDetailResponse>(`/media/${id}`);
}

export async function getFriends(): Promise<FriendsResponse> {
  return serverFetch<FriendsResponse>('/friends');
}

export async function getActivity(
  scope: 'all' | 'mine' | 'friends' = 'all',
  limit = 200,
): Promise<ActivityResponse> {
  return serverFetch<ActivityResponse>(`/activity?scope=${scope}&limit=${limit}`);
}

export async function getUserActivity(userId: number, limit = 100): Promise<ActivityResponse> {
  return serverFetch<ActivityResponse>(`/activity?userId=${userId}&limit=${limit}`);
}

export async function getUserProfile(id: number | string): Promise<PublicProfileResponse> {
  return serverFetch<PublicProfileResponse>(`/users/${id}`);
}

// Deduped per request — called by both the root layout and page components.
export const getMe = cache((): Promise<UserProfileResponse> =>
  serverFetch<UserProfileResponse>('/user/me'),
);
