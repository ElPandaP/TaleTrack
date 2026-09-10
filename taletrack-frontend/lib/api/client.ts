import { API_CONFIG } from './config';

/** Error thrown for a failed request. `code` is a backend-supplied stable key
 *  (when present) that the UI can map to a localized message. */
export class ApiError extends Error {
  status: number;
  code?: string;
  constructor(message: string, status: number, code?: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

const TOKEN_KEY = 'token';
const REFRESH_KEY = 'tt-refresh';
const COOKIE_MAX_AGE = 60 * 60 * 24 * 30; // 30 days

export class ApiClient {
  private baseURL: string;
  /** Shared in-flight refresh so parallel 401s trigger only one /auth/refresh. */
  private refreshInFlight: Promise<boolean> | null = null;

  constructor() {
    this.baseURL = API_CONFIG.baseURL;
  }

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

  private getToken(): string | null {
    if (typeof window === 'undefined') return null;
    return localStorage.getItem(TOKEN_KEY);
  }

  public getRefreshToken(): string | null {
    if (typeof window === 'undefined') return null;
    return localStorage.getItem(REFRESH_KEY);
  }

  public setToken(token: string, refreshToken?: string): void {
    if (typeof window === 'undefined') return;
    localStorage.setItem(TOKEN_KEY, token);
    document.cookie = `tt-token=${token}; path=/; SameSite=Lax; max-age=${COOKIE_MAX_AGE}`;
    if (refreshToken) {
      localStorage.setItem(REFRESH_KEY, refreshToken);
      document.cookie = `tt-refresh=${refreshToken}; path=/; SameSite=Lax; max-age=${COOKIE_MAX_AGE}`;
    }
  }

  public clearToken(): void {
    if (typeof window === 'undefined') return;
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
    document.cookie = 'tt-token=; path=/; max-age=0';
    document.cookie = 'tt-refresh=; path=/; max-age=0';
  }

  /**
   * Exchange the stored refresh token for a fresh access + refresh pair.
   * Returns false (and clears tokens) when there is no refresh token or it is rejected.
   * Concurrent callers share a single request.
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
        // Network hiccup — keep tokens, let the caller surface the error.
        return false;
      } finally {
        this.refreshInFlight = null;
      }
    })();

    return this.refreshInFlight;
  }

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

      // Access token likely expired — refresh once and replay the request.
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

      const expectedCodes = [400, 401, 403, 409, 422, 429];

      if (expectedCodes.includes(status)) {
        const body = await response.text();
        let message = 'Request failed.';
        let code: string | undefined;
        try {
          const json = JSON.parse(body);
          message = json.message ?? json.error ?? json.title ?? message;
          code = typeof json.code === 'string' ? json.code : undefined;
        } catch { /* use default */ }
        throw new ApiError(message, status, code);
      }

      let reason = 'Something went wrong';
      try {
        const noRes = await fetch('https://naas.isalman.dev/no');
        if (noRes.ok) {
          const noData = await noRes.json();
          reason = noData.reason ?? reason;
        }
      } catch { /* use default reason */ }
      throw new Error(`${status} Reason: ${reason}`);
    }

    return response.json();
  }

  async get<T>(endpoint: string, requireAuth: boolean = false): Promise<T> {
    return this.request<T>(endpoint, { method: 'GET' }, requireAuth);
  }

  async post<T>(endpoint: string, body: unknown, requireAuth: boolean = false): Promise<T> {
    return this.request<T>(endpoint, { method: 'POST', body: JSON.stringify(body) }, requireAuth);
  }

  async postForm<T>(endpoint: string, form: FormData, requireAuth: boolean = false): Promise<T> {
    return this.request<T>(endpoint, { method: 'POST', body: form }, requireAuth);
  }

  async put<T>(endpoint: string, body: unknown, requireAuth: boolean = false): Promise<T> {
    return this.request<T>(endpoint, { method: 'PUT', body: JSON.stringify(body) }, requireAuth);
  }

  async delete<T>(endpoint: string, requireAuth: boolean = false): Promise<T> {
    return this.request<T>(endpoint, { method: 'DELETE' }, requireAuth);
  }
}

export const apiClient = new ApiClient();
