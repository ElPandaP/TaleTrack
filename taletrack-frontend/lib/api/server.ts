import { cookies } from 'next/headers';
import { API_CONFIG } from './config';
import type { GetTrackingEventsResponse, GetUserBooksResponse } from '../types';

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

  const res = await fetch(`${API_CONFIG.baseURL}${endpoint}`, {
    headers,
    cache: 'no-store', // user-specific data — never cache across requests
  });

  if (!res.ok) throw new Error(`API ${res.status}`);
  return res.json() as Promise<T>;
}

export async function getTrackingEvents(
  type?: string,
  limit = 200,
): Promise<GetTrackingEventsResponse> {
  const params = new URLSearchParams({ limit: String(limit), orderBy: 'desc' });
  if (type) params.set('type', type);
  return serverFetch<GetTrackingEventsResponse>(`/tracking?${params}`, true);
}

export async function getBooks(): Promise<GetUserBooksResponse> {
  return serverFetch<GetUserBooksResponse>('/books');
}
