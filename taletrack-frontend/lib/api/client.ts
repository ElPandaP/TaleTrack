import { API_CONFIG } from './config';
import { ACCESS_TOKEN_COOKIE, ACCESS_TOKEN_KEY, AUTH_COOKIE_MAX_AGE, REFRESH_TOKEN_COOKIE, REFRESH_TOKEN_KEY } from '@/lib/auth-storage';

/** Error thrown when the backend answers with an error status. */
export class ApiError extends Error {
  /** HTTP status code of the response. */
  status: number;
  /** Stable machine-readable error code from the backend, when it sends one; the UI maps it to a translated message. */
  code?: string;
  constructor(message: string, status: number, code?: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}


/**
 * Browser-side HTTP client for the backend API.
 *
 * @remarks
 * Keeps the access and refresh tokens in `localStorage`, mirrored in the `tt-token` and
 * `tt-refresh` cookies so the middleware and Server Components can read them. Authenticated
 * requests send the access token as a bearer token; on a 401 the client refreshes the pair
 * once and replays the request. Use the shared {@link apiClient} instance.
 */
export class ApiClient {
  private baseURL: string;
  /** Shared in-flight refresh so parallel 401s trigger only one `/auth/refresh` call. */
  private refreshInFlight: Promise<boolean> | null = null;

  constructor() {
    this.baseURL = API_CONFIG.baseURL;
  }

  /** Builds the JSON headers, adding the bearer token when `includeAuth` is set and a token exists. */
  private getHeaders(includeAuth: boolean = false): HeadersInit {
    const headers: HeadersInit = {
      'Content-Type': 'application/json',
    };

    if (includeAuth) {
      const token = this.getToken();
      if (token) {
        headers['Authorization'] = `Bearer ${token}`;
      }
    }

    return headers;
  }

  /** Reads the stored access token; always `null` on the server. */
  private getToken(): string | null {
    if (typeof window === 'undefined') return null;
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  /** Reads the stored refresh token; always `null` on the server. */
  public getRefreshToken(): string | null {
    if (typeof window === 'undefined') return null;
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  /**
   * Stores a new access token, and optionally a new refresh token, in `localStorage` and in
   * their cookies.
   *
   * @param token - The access token.
   * @param refreshToken - The refresh token; when omitted the stored one is kept.
   */
  public setToken(token: string, refreshToken?: string): void {
    if (typeof window === 'undefined') return;
    localStorage.setItem(ACCESS_TOKEN_KEY, token);
    document.cookie = `${ACCESS_TOKEN_COOKIE}=${token}; path=/; SameSite=Lax; max-age=${AUTH_COOKIE_MAX_AGE}`;
    if (refreshToken) {
      localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
      document.cookie = `${REFRESH_TOKEN_COOKIE}=${refreshToken}; path=/; SameSite=Lax; max-age=${AUTH_COOKIE_MAX_AGE}`;
    }
  }

  /**
   * Asks the server to revoke a refresh token, by default this device's own. Best effort: the
   * caller does not wait for it, and `keepalive` lets the request outlive a page navigation
   * right after logout.
   *
   * @param refreshToken - The token to revoke; defaults to the stored one.
   */
  public async revokeRefreshToken(refreshToken: string | null = this.getRefreshToken()): Promise<void> {
    if (!refreshToken) return;
    try {
      await fetch(`${this.baseURL}/auth/logout`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
        keepalive: true,
      });
    } catch {
      // Offline: the session simply expires on its own.
    }
  }

  /** Removes both tokens from `localStorage` and expires their cookies. */
  public clearToken(): void {
    if (typeof window === 'undefined') return;
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    document.cookie = `${ACCESS_TOKEN_COOKIE}=; path=/; max-age=0`;
    document.cookie = `${REFRESH_TOKEN_COOKIE}=; path=/; max-age=0`;
  }

  /**
   * Exchanges the stored refresh token for a fresh access and refresh pair. Concurrent callers
   * share a single request.
   *
   * @returns `true` when new tokens were stored. `false` when there is no refresh token or the
   * server rejected it (the tokens are then cleared), or when the network failed (the tokens
   * are kept).
   */
  public refresh(): Promise<boolean> {
    if (this.refreshInFlight) return this.refreshInFlight;

    this.refreshInFlight = (async () => {
      const refreshToken = this.getRefreshToken();
      if (!refreshToken) return false;

      try {
        const res = await fetch(`${this.baseURL}/auth/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken }),
        });
        if (!res.ok) {
          this.clearToken();
          return false;
        }
        const data = (await res.json()) as { token?: string; refreshToken?: string };
        if (!data.token) {
          this.clearToken();
          return false;
        }
        this.setToken(data.token, data.refreshToken);
        return true;
      } catch {
        // Network failure: keep the tokens and let the caller surface the error.
        return false;
      } finally {
        this.refreshInFlight = null;
      }
    })();

    return this.refreshInFlight;
  }

  /**
   * Sends a request to `baseURL + endpoint` and parses the JSON response.
   *
   * @param endpoint - Path relative to the API base, e.g. `/library`.
   * @param options - Extra `fetch` options; a `FormData` body is sent as multipart.
   * @param requireAuth - Whether to send the access token (and refresh it on a 401).
   * @param _retried - Internal flag that stops a replayed request from refreshing again.
   * @returns The parsed response body.
   * @throws {@link ApiError} when the backend answers with an error status.
   */
  async request<T>(
    endpoint: string,
    options: RequestInit = {},
    requireAuth: boolean = false,
    _retried: boolean = false,
  ): Promise<T> {
    const url = `${this.baseURL}${endpoint}`;
    const headers = this.getHeaders(requireAuth) as Record<string, string>;

    // Let the browser set multipart/form-data with its boundary.
    if (options.body instanceof FormData) delete headers['Content-Type'];

    const response = await fetch(url, {
      ...options,
      headers: {
        ...headers,
        ...options.headers,
      },
    });

    if (!response.ok) {
      const status = response.status;

      // The access token has probably expired: refresh once and replay the request.
      if (
        status === 401 &&
        requireAuth &&
        !_retried &&
        endpoint !== '/auth/refresh' &&
        this.getRefreshToken()
      ) {
        const refreshed = await this.refresh();
        if (refreshed) {
          return this.request<T>(endpoint, options, requireAuth, true);
        }
      }

      // The body is { message, code? } for API errors and ProblemDetails for unhandled ones.
      const body = await response.text();
      let message = 'Request failed.';
      let code: string | undefined;
      try {
        const json = JSON.parse(body);
        message = json.message ?? json.error ?? json.title ?? message;
        code = typeof json.code === 'string' ? json.code : undefined;
      } catch { /* not JSON: keep the default message */ }
      throw new ApiError(message, status, code);
    }

    return response.json();
  }

  /** Sends a GET request. See {@link ApiClient.request}. */
  async get<T>(endpoint: string, requireAuth: boolean = false): Promise<T> {
    return this.request<T>(endpoint, { method: 'GET' }, requireAuth);
  }

  /** Sends a POST request with `body` as JSON. See {@link ApiClient.request}. */
  async post<T>(endpoint: string, body: unknown, requireAuth: boolean = false): Promise<T> {
    return this.request<T>(endpoint, { method: 'POST', body: JSON.stringify(body) }, requireAuth);
  }

  /** Sends a PUT request with `body` as JSON. See {@link ApiClient.request}. */
  async put<T>(endpoint: string, body: unknown, requireAuth: boolean = false): Promise<T> {
    return this.request<T>(endpoint, { method: 'PUT', body: JSON.stringify(body) }, requireAuth);
  }

  /** Sends a PUT request with a multipart form body (file uploads). See {@link ApiClient.request}. */
  async putForm<T>(endpoint: string, form: FormData, requireAuth: boolean = false): Promise<T> {
    return this.request<T>(endpoint, { method: 'PUT', body: form }, requireAuth);
  }

  /** Sends a DELETE request. See {@link ApiClient.request}. */
  async delete<T>(endpoint: string, requireAuth: boolean = false): Promise<T> {
    return this.request<T>(endpoint, { method: 'DELETE' }, requireAuth);
  }
}

/** The shared {@link ApiClient} instance used by every service and component. */
export const apiClient = new ApiClient();
