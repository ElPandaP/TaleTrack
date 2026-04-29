import { API_CONFIG } from './config';

export class ApiClient {
  private baseURL: string;
  private internalApiKey: string;

  constructor() {
    this.baseURL = API_CONFIG.baseURL;
    this.internalApiKey = API_CONFIG.internalApiKey;
  }

  private getHeaders(includeAuth: boolean = false, includeApiKey: boolean = false): HeadersInit {
    const headers: HeadersInit = {
      'Content-Type': 'application/json',
    };

    if (includeAuth) {
      const token = this.getToken();
      if (token) {
        headers['Authorization'] = `Bearer ${token}`;
      }
    }

    if (includeApiKey && this.internalApiKey) {
      headers['X-Internal-Api-Key'] = this.internalApiKey;
    }

    return headers;
  }

  private getToken(): string | null {
    if (typeof window === 'undefined') return null;
    return localStorage.getItem('token');
  }

  public setToken(token: string): void {
    if (typeof window === 'undefined') return;
    localStorage.setItem('token', token);
    const maxAge = 60 * 60 * 24 * 30; // 30 days
    document.cookie = `tt-token=${token}; path=/; SameSite=Lax; max-age=${maxAge}`;
  }

  public clearToken(): void {
    if (typeof window === 'undefined') return;
    localStorage.removeItem('token');
    document.cookie = 'tt-token=; path=/; max-age=0';
  }

  async request<T>(
    endpoint: string,
    options: RequestInit = {},
    requireAuth: boolean = false,
    requireApiKey: boolean = false
  ): Promise<T> {
    const url = `${this.baseURL}${endpoint}`;
    const headers = this.getHeaders(requireAuth, requireApiKey);

    const response = await fetch(url, {
      ...options,
      headers: {
        ...headers,
        ...options.headers,
      },
    });

    if (!response.ok) {
      const status = response.status;
      const expectedCodes = [400, 401, 403, 409, 422, 429];

      if (expectedCodes.includes(status)) {
        const body = await response.text();
        let message = 'Request failed.';
        try {
          const json = JSON.parse(body);
          message = json.message ?? json.error ?? json.title ?? message;
        } catch { /* use default */ }
        throw new Error(message);
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

  async get<T>(endpoint: string, requireAuth: boolean = false, requireApiKey: boolean = false): Promise<T> {
    return this.request<T>(endpoint, { method: 'GET' }, requireAuth, requireApiKey);
  }

  async post<T>(
    endpoint: string,
    body: unknown,
    requireAuth: boolean = false,
    requireApiKey: boolean = false
  ): Promise<T> {
    return this.request<T>(
      endpoint,
      {
        method: 'POST',
        body: JSON.stringify(body),
      },
      requireAuth,
      requireApiKey
    );
  }

  async put<T>(
    endpoint: string,
    body: unknown,
    requireAuth: boolean = false,
    requireApiKey: boolean = false
  ): Promise<T> {
    return this.request<T>(
      endpoint,
      {
        method: 'PUT',
        body: JSON.stringify(body),
      },
      requireAuth,
      requireApiKey
    );
  }

  async delete<T>(endpoint: string, requireAuth: boolean = false, requireApiKey: boolean = false): Promise<T> {
    return this.request<T>(endpoint, { method: 'DELETE' }, requireAuth, requireApiKey);
  }
}

export const apiClient = new ApiClient();
