'use client';

import { createContext, useContext, useEffect, useRef, useSyncExternalStore } from 'react';
import { authService } from '@/lib/api/services';
import { apiClient } from '@/lib/api/client';
import { decodeJwtPayload, isJwtValid } from '@/lib/jwt';
import { ACCESS_TOKEN_COOKIE, ACCESS_TOKEN_KEY, REFRESH_TOKEN_KEY } from '@/lib/auth-storage';

/** The signed-in user, as read from the access token's claims. */
export interface AuthUser {
  /** User id (`sub` claim). */
  id: string;
  /** Username (`unique_name` claim). */
  username: string;
  /** Email address (`email` claim). */
  email: string;
}

/** What {@link useAuth} returns. */
export interface AuthContextType {
  /** Whether a valid, unexpired access token is stored. */
  isAuthenticated: boolean;
  /** True while the session state is not known yet: during hydration, or while an expired token is being refreshed. */
  loading: boolean;
  /** The signed-in user, or `null` when signed out. */
  user: AuthUser | null;
  /** Call after the auth service has stored a new token, so every subscriber re-reads the session. */
  login: () => void;
  /** Revokes this device's session, drops the tokens and notifies subscribers. */
  logout: () => void;
}

const AuthContext = createContext<AuthContextType>({
  isAuthenticated: false,
  loading: true,
  user: null,
  login: () => {},
  logout: () => {},
});

/**
 * Decodes a JWT's payload without checking its signature.
 *
 * @param token - The access token.
 * @returns The claims the frontend uses, or `null` if the token cannot be decoded.
 */
export function parseJwt(
  token: string,
): { email?: string; unique_name?: string; sub?: string; exp?: number } | null {
  try {
    return decodeJwtPayload(token);
  } catch {
    return null;
  }
}


// --- External store ---
// The session lives in localStorage; React reads it through useSyncExternalStore.

const authListeners = new Set<() => void>();

/**
 * Tells every {@link useAuth} subscriber in this tab to re-read the stored tokens. Call it
 * after changing the tokens outside of `login`/`logout`, e.g. after a profile edit issues a
 * new access token.
 */
export function notifyAuthChange() {
  authListeners.forEach((fn) => fn());
}

/** Subscribes to session changes made in this tab or, through the `storage` event, in another one. */
function subscribeAuth(callback: () => void) {
  authListeners.add(callback);
  // Sign-in, sign-out and token rotation in another tab only reach this one
  // through the storage event (key is null when the whole storage is cleared).
  const onStorage = (e: StorageEvent) => {
    if (e.key === null || e.key === ACCESS_TOKEN_KEY || e.key === REFRESH_TOKEN_KEY) callback();
  };
  window.addEventListener('storage', onStorage);
  return () => {
    authListeners.delete(callback);
    window.removeEventListener('storage', onStorage);
  };
}

/** The three possible session states the store can report. */
type AuthSnapshot =
  | { isAuthenticated: false; loading: false; user: null }
  | { isAuthenticated: false; loading: true; user: null }
  | { isAuthenticated: true; loading: false; user: AuthUser };

const UNAUTHENTICATED: AuthSnapshot = { isAuthenticated: false, loading: false, user: null };
const REFRESHING: AuthSnapshot = { isAuthenticated: false, loading: true, user: null };

// useSyncExternalStore needs a stable object while nothing changed, so the snapshot is
// cached and only rebuilt when the stored token differs from the last one seen.
let cachedToken: string | null | undefined = undefined;
let cachedSnapshot: AuthSnapshot = UNAUTHENTICATED;

/** Builds the session state from the stored access token, logging out if it expired and cannot be refreshed. */
function getAuthSnapshot(): AuthSnapshot {
  const token = localStorage.getItem(ACCESS_TOKEN_KEY);
  if (token === cachedToken) return cachedSnapshot;
  cachedToken = token;
  if (!token) {
    cachedSnapshot = UNAUTHENTICATED;
    return cachedSnapshot;
  }
  if (!isJwtValid(token)) {
    // Expired access token. With a refresh token, report a loading state while
    // AuthProvider's effect swaps it for a fresh one; without one, log out.
    if (localStorage.getItem(REFRESH_TOKEN_KEY)) {
      cachedSnapshot = REFRESHING;
      return cachedSnapshot;
    }
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    document.cookie = `${ACCESS_TOKEN_COOKIE}=; path=/; max-age=0`;
    cachedToken = null;
    cachedSnapshot = UNAUTHENTICATED;
    return cachedSnapshot;
  }
  const decoded = parseJwt(token);
  cachedSnapshot = {
    isAuthenticated: true,
    loading: false,
    user: {
      id: decoded?.sub ?? '',
      username: decoded?.unique_name ?? 'User',
      email: decoded?.email ?? '',
    },
  };
  return cachedSnapshot;
}

/** The server (and hydration) render always sees a signed-out session, since it cannot read `localStorage`. */
function getServerAuthSnapshot() {
  return UNAUTHENTICATED;
}

// --- Provider ---

/**
 * Provides the session state to every component below it and refreshes an expired access
 * token in the background when a refresh token is available.
 */
export function AuthProvider({ children }: { children: React.ReactNode }) {
  const state = useSyncExternalStore(subscribeAuth, getAuthSnapshot, getServerAuthSnapshot);
  const refreshing = useRef(false);

  // Access token expired but a refresh token is present: exchange it once.
  // apiClient.refresh() clears the tokens itself on a real rejection but keeps them
  // on a network error, so a flaky connection does not log the user out. They must
  // not be cleared here just because the call returned false.
  useEffect(() => {
    if (!state.loading || refreshing.current) return;
    refreshing.current = true;
    apiClient
      .refresh()
      .finally(() => {
        refreshing.current = false;
        notifyAuthChange();
      });
  }, [state.loading]);

  const login = () => notifyAuthChange();

  const logout = () => {
    authService.logout();
    notifyAuthChange();
  };

  return (
    <AuthContext.Provider value={{ ...state, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

/** Returns the current session state plus the `login` and `logout` actions. */
export function useAuth() {
  return useContext(AuthContext);
}
