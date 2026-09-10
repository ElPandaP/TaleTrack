// Token lifecycle for the extension: stored in chrome.storage.local, refreshed
// against the TaleTrack backend, obtained via the web app's /extension-auth page.

import { BACKEND_URL, FRONTEND_URL } from './config';
import type { AuthState } from './types';

interface Tokens {
  access: string;
  refresh: string;
  expiresAt: number; // epoch ms
}

const REFRESH_SKEW_MS = 2 * 60 * 1000; // refresh when <2 min of life left

let refreshInFlight: Promise<string | null> | null = null;

async function readTokens(): Promise<Tokens | null> {
  const o: Record<string, unknown> = await chrome.storage.local.get([
    'tt_access',
    'tt_refresh',
    'tt_expires_at',
  ]);
  const access = o.tt_access;
  const refresh = o.tt_refresh;
  if (typeof access !== 'string' || typeof refresh !== 'string') return null;
  return {
    access,
    refresh,
    expiresAt: typeof o.tt_expires_at === 'number' ? o.tt_expires_at : 0,
  };
}

async function writeTokens(t: Tokens): Promise<void> {
  await chrome.storage.local.set({
    tt_access: t.access,
    tt_refresh: t.refresh,
    tt_expires_at: t.expiresAt,
  });
}

export async function clearTokens(): Promise<void> {
  await chrome.storage.local.remove(['tt_access', 'tt_refresh', 'tt_expires_at']);
}

/** Trade the stored refresh token for a fresh pair. Clears tokens on hard failure. */
function refresh(): Promise<string | null> {
  if (refreshInFlight) return refreshInFlight;

  refreshInFlight = (async () => {
    const tokens = await readTokens();
    if (!tokens) return null;
    try {
      const res = await fetch(`${BACKEND_URL}/api/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: tokens.refresh }),
      });
      if (!res.ok) {
        if (res.status === 401) await clearTokens();
        return null;
      }
      const data = (await res.json()) as { token?: string; refreshToken?: string; expiresIn?: number };
      if (!data.token || !data.refreshToken) {
        await clearTokens();
        return null;
      }
      await writeTokens({
        access: data.token,
        refresh: data.refreshToken,
        expiresAt: Date.now() + (data.expiresIn ?? 3600) * 1000,
      });
      return data.token;
    } catch {
      return null; // network blip — keep tokens, try again later
    } finally {
      refreshInFlight = null;
    }
  })();

  return refreshInFlight;
}

/** A usable access token, refreshing first if it's about to expire. Null = signed out. */
export async function getValidAccessToken(): Promise<string | null> {
  const tokens = await readTokens();
  if (!tokens) return null;
  if (tokens.expiresAt - Date.now() > REFRESH_SKEW_MS) return tokens.access;
  return refresh();
}

/** Authenticated fetch against the backend, with one refresh-and-retry on 401. */
export async function apiFetch(path: string, init: RequestInit = {}): Promise<Response | null> {
  let token = await getValidAccessToken();
  if (!token) return null;

  const call = (t: string) =>
    fetch(`${BACKEND_URL}${path}`, {
      ...init,
      headers: { 'Content-Type': 'application/json', ...init.headers, Authorization: `Bearer ${t}` },
    });

  let res = await call(token);
  if (res.status === 401) {
    token = await refresh();
    if (!token) return null;
    res = await call(token);
  }
  return res;
}

/** Kick off the web-app authorisation flow and store the returned token pair. */
export async function signIn(): Promise<boolean> {
  const redirectUri = chrome.identity.getRedirectURL();
  const authUrl = `${FRONTEND_URL}/extension-auth?redirect_uri=${encodeURIComponent(redirectUri)}`;

  const resultUrl = await chrome.identity.launchWebAuthFlow({ url: authUrl, interactive: true });
  if (!resultUrl) return false;

  const frag = new URL(resultUrl).hash.replace(/^#/, '');
  const p = new URLSearchParams(frag);
  const access = p.get('access_token');
  const refreshToken = p.get('refresh_token');
  const expiresIn = Number(p.get('expires_in') ?? '3600');
  if (!access || !refreshToken) return false;

  await writeTokens({ access, refresh: refreshToken, expiresAt: Date.now() + expiresIn * 1000 });
  return true;
}

/** Revoke this device's session server-side (best effort), then drop local tokens. */
export async function signOut(): Promise<void> {
  const tokens = await readTokens();
  if (tokens) {
    try {
      await fetch(`${BACKEND_URL}/api/auth/logout`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: tokens.refresh }),
      });
    } catch {
      /* offline — the session will just expire on its own */
    }
  }
  await clearTokens();
}

export async function getAuthState(): Promise<AuthState> {
  const token = await getValidAccessToken();
  if (!token) return { authenticated: false };

  try {
    const res = await apiFetch('/api/user/me', { method: 'GET' });
    if (res && res.ok) {
      const body = (await res.json()) as { data?: { username: string; email: string } };
      if (body.data) return { authenticated: true, user: body.data };
    }
  } catch {
    /* fall through */
  }
  return { authenticated: true };
}
