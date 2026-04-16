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
  }

  public clearToken(): void {
    if (typeof window === 'undefined') return;
    localStorage.removeItem('token');
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
      const errorText = await response.text();
      throw new Error(`API Error: ${response.status} - ${errorText}`);
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
