/**
 * Session handling for the extension, used by the background service worker.
 *
 * The extension holds its own access and refresh token pair, issued by the web app's
 * `/extension-auth` page and stored in `chrome.storage.local`. This module reads and writes that
 * pair, refreshes it against the backend before it expires, makes authenticated API calls and
 * signs out.
 *
 * @module
 */

import { BACKEND_URL, FRONTEND_URL, debug } from './config';
import type { AuthState, SessionChangedMessage } from './types';

/** The stored token pair. */
interface Tokens {
  access: string;
  refresh: string;
  /** When the access token expires, in epoch milliseconds. */
  expiresAt: number;
}

/** Refresh the access token once it has less than this much life left (2 minutes). */
const REFRESH_SKEW_MS = 2 * 60 * 1000;

/** The refresh request under way, shared so concurrent callers don't each rotate the token. */
let refreshInFlight: Promise<string | null> | null = null;

/** Reads the token pair from `chrome.storage.local`, or null when signed out. */
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

/** Saves the token pair to `chrome.storage.local`. */
async function writeTokens(t: Tokens): Promise<void> {
  await chrome.storage.local.set({
    tt_access: t.access,
    tt_refresh: t.refresh,
    tt_expires_at: t.expiresAt,
  });
}

/** Whether a token pair is stored, i.e. the extension is signed in. */
export async function hasSession(): Promise<boolean> {
  return (await readTokens()) !== null;
}

/**
 * Tells the content script of every open Netflix tab that the extension signed in or out, so it
 * starts or stops tracking. Tabs without a content script (still loading) are skipped.
 *
 * @param signedIn - Whether the extension is now signed in.
 */
async function notifyNetflixTabs(signedIn: boolean): Promise<void> {
  const message: SessionChangedMessage = { type: 'SESSION_CHANGED', signedIn };
  const tabs = await chrome.tabs.query({ url: 'https://www.netflix.com/*' });
  for (const tab of tabs) {
    if (tab.id === undefined) continue;
    chrome.tabs.sendMessage(tab.id, message).catch(() => {
      /* no content script in that tab yet: it asks for the session itself when it loads */
    });
  }
}

/** Removes the stored token pair, leaving the extension signed out, and stops tracking in open Netflix tabs. */
async function clearTokens(): Promise<void> {
  await chrome.storage.local.remove(['tt_access', 'tt_refresh', 'tt_expires_at']);
  await notifyNetflixTabs(false);
}

/**
 * Trades the stored refresh token for a fresh pair (`POST /api/auth/refresh`).
 *
 * Concurrent calls share a single request. When the backend rejects the refresh token (the session
 * was revoked or has expired) the tokens are cleared; a network failure keeps them so a later
 * attempt can succeed.
 *
 * @returns The new access token, or null when there is none.
 */
function refresh(): Promise<string | null> {
  if (refreshInFlight) return refreshInFlight;

  refreshInFlight = (async () => {
    const tokens = await readTokens();
    if (!tokens) return null;
    debug('refreshing the access token');
    try {
      const res = await fetch(`${BACKEND_URL}/api/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: tokens.refresh }),
      });
      if (!res.ok) {
        debug('refresh failed:', res.status, res.status === 401 ? '(session revoked/expired, signing out)' : '');
        if (res.status === 401) await clearTokens();
        return null;
      }
      const data = (await res.json()) as { token?: string; refreshToken?: string; expiresIn?: number };
      if (!data.token || !data.refreshToken) {
        debug('refresh answered without a token pair, signing out');
        await clearTokens();
        return null;
      }
      await writeTokens({
        access: data.token,
        refresh: data.refreshToken,
        expiresAt: Date.now() + (data.expiresIn ?? 3600) * 1000,
      });
      return data.token;
    } catch (err) {
      debug('refresh could not reach', BACKEND_URL, ':', err);
      return null;
    } finally {
      refreshInFlight = null;
    }
  })();

  return refreshInFlight;
}

/**
 * A usable access token, refreshed first when it is about to expire.
 *
 * @returns The access token, or null when signed out or the refresh failed.
 */
export async function getValidAccessToken(): Promise<string | null> {
  const tokens = await readTokens();
  if (!tokens) {
    debug('no tokens stored: signed out');
    return null;
  }
  if (tokens.expiresAt - Date.now() > REFRESH_SKEW_MS) return tokens.access;
  return refresh();
}

/**
 * Authenticated JSON request to the backend. On a 401 it refreshes the token and retries once.
 *
 * @param path - API path, starting with `/api/`.
 * @param init - Fetch options; the `Authorization` and JSON `Content-Type` headers are added.
 * @returns The response, or null when there is no session to send it with.
 */
export async function apiFetch(path: string, init: RequestInit = {}): Promise<Response | null> {
  let token = await getValidAccessToken();
  if (!token) return null;

  const call = (t: string) =>
    fetch(`${BACKEND_URL}${path}`, {
      ...init,
      headers: { 'Content-Type': 'application/json', ...init.headers, Authorization: `Bearer ${t}` },
    });

  debug(init.method ?? 'GET', `${BACKEND_URL}${path}`);
  let res = await call(token);
  if (res.status === 401) {
    debug('401 from', path, ', refreshing and retrying once');
    token = await refresh();
    if (!token) return null;
    res = await call(token);
  }
  return res;
}

/**
 * Opens the web app's `/extension-auth` page, where the user confirms and the page issues the
 * extension's tokens.
 *
 * @remarks
 * The tokens come back later through {@link handleExternalAuthMessage}. Nothing waits for them
 * here, because the popup closes as soon as the tab takes focus and the service worker may be
 * asleep by the time they arrive.
 */
export function openSignInTab(): void {
  chrome.tabs.create({ url: `${FRONTEND_URL}/extension-auth` });
}

/**
 * Stores the token pair received from the `/extension-auth` page.
 *
 * @remarks
 * Only the background service worker's external message listener calls this, after it has checked
 * the sender's origin and the message shape. Token values from anywhere else must never reach it.
 *
 * @param access - Access token (JWT).
 * @param refresh - Refresh token.
 * @param expiresIn - Access token lifetime in seconds.
 */
export async function handleExternalAuthMessage(access: string, refresh: string, expiresIn = 3600): Promise<void> {
  await writeTokens({ access, refresh, expiresAt: Date.now() + expiresIn * 1000 });
  await notifyNetflixTabs(true);
}

/**
 * Signs out: revokes this device's session on the backend (`POST /api/auth/logout`, best effort)
 * and drops the local tokens.
 */
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
      /* offline: the session simply expires on the backend */
    }
  }
  await clearTokens();
}

/**
 * The session state shown in the popup, with the account's username and email when available.
 *
 * @remarks
 * It always renews the session first, so a connection revoked from the web app shows as signed out
 * as soon as the popup opens. A network failure keeps the stored tokens and falls back to them.
 */
export async function getAuthState(): Promise<AuthState> {
  const token = (await refresh()) ?? (await getValidAccessToken());
  if (!token) return { authenticated: false };

  try {
    const res = await apiFetch('/api/users/me', { method: 'GET' });
    if (res && res.ok) {
      const body = (await res.json()) as { data?: { username: string; email: string } };
      if (body.data) return { authenticated: true, user: body.data };
    }
  } catch {
    /* the account details are optional: still signed in without them */
  }
  return { authenticated: true };
}
