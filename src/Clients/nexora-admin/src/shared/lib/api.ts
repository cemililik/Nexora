import axios, {
  type AxiosError,
  type AxiosInstance,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from 'axios';

import type { ApiEnvelope } from '@/shared/types/api';
import { useAuthStore } from '@/shared/lib/stores/authStore';
import { getKeycloak } from '@/shared/lib/auth';

let currentLanguage = 'en';

/** Called by i18n setup to keep Accept-Language in sync. */
export function setApiLanguage(lang: string): void {
  currentLanguage = lang;
  apiClient.defaults.headers.common['Accept-Language'] = lang;
}

let refreshPromise: Promise<boolean> | null = null;

function createApiClient(): AxiosInstance {
  const client = axios.create({
    baseURL: import.meta.env.VITE_API_BASE_URL ?? '/api/v1',
    timeout: Number(import.meta.env.VITE_API_TIMEOUT_MS) || 10_000,
    headers: {
      'Content-Type': 'application/json',
      'Accept-Language': currentLanguage,
    },
  });

  client.interceptors.response.use(
    (response) => response,
    async (error: AxiosError) => {
      const originalRequest = error.config as
        | (InternalAxiosRequestConfig & { _retry?: boolean })
        | undefined;

      if (
        error.response?.status === 401 &&
        typeof window !== 'undefined' &&
        originalRequest &&
        !originalRequest._retry
      ) {
        originalRequest._retry = true;

        const kc = getKeycloak();
        if (kc) {
          if (!refreshPromise) {
            refreshPromise = kc
              .updateToken(5)
              .then(() => true)
              .catch(() => false)
              .finally(() => {
                refreshPromise = null;
              });
          }

          const refreshed = await refreshPromise;

          if (refreshed && kc.token) {
            setAuthToken(kc.token);
            originalRequest.headers['Authorization'] = `Bearer ${kc.token}`;
            return client(originalRequest);
          }
        }

        setAuthToken(null);
        useAuthStore.getState().clearSession();
        if (window.location.pathname !== '/login') {
          window.location.href = '/login';
        }
      }
      return Promise.reject(error);
    },
  );

  return client;
}

const apiClient = createApiClient();

/**
 * Set the Authorization header for all subsequent requests.
 * Called after authentication to inject the JWT token.
 */
export function setAuthToken(token: string | null): void {
  if (token) {
    apiClient.defaults.headers.common['Authorization'] = `Bearer ${token}`;
  } else {
    delete apiClient.defaults.headers.common['Authorization'];
  }
}

function unwrapEnvelope<T>(data: ApiEnvelope<T>): T {
  // data.data can be null for void responses (PUT/DELETE with no body)
  return data.data as T;
}

/** Typed API helpers that unwrap ApiEnvelope automatically. */
export const api = {
  async get<T>(url: string, params?: Record<string, unknown>): Promise<T> {
    const response = await apiClient.get<ApiEnvelope<T>>(url, { params });
    return unwrapEnvelope(response.data);
  },

  async post<T>(
    url: string,
    data?: unknown,
    config?: AxiosRequestConfig,
  ): Promise<T> {
    const response = await apiClient.post<ApiEnvelope<T>>(url, data, config);
    return unwrapEnvelope(response.data);
  },

  async put<T>(url: string, data?: unknown): Promise<T> {
    const response = await apiClient.put<ApiEnvelope<T>>(url, data);
    return unwrapEnvelope(response.data);
  },

  async patch<T>(url: string, data?: unknown): Promise<T> {
    const response = await apiClient.patch<ApiEnvelope<T>>(url, data);
    return unwrapEnvelope(response.data);
  },

  async delete<T = void>(url: string): Promise<T> {
    const resp = await apiClient.delete<ApiEnvelope<T>>(url);
    if (resp.status === 204 || resp.data?.data == null) {
      return undefined as T;
    }
    return resp.data.data;
  },

  async blob(url: string): Promise<Blob> {
    const response = await apiClient.get<Blob>(url, { responseType: 'blob' });
    return response.data;
  },

  /**
   * Get the raw ApiEnvelope response (for cases where you need
   * the message, meta, or validation errors).
   */
  async getRaw<T>(
    url: string,
    params?: Record<string, unknown>,
  ): Promise<ApiEnvelope<T>> {
    const response = await apiClient.get<ApiEnvelope<T>>(url, { params });
    return response.data;
  },
};

/**
 * Extract error info from an API error response.
 * Returns the localization key, meta params, and validation errors.
 */
export function extractApiError(error: unknown): {
  message: string;
  meta?: Record<string, string>;
  errors?: Array<{ key: string; params?: Record<string, string> }>;
  status?: number;
} {
  if (!axios.isAxiosError(error)) {
    return { message: 'lockey_error_unexpected' };
  }

  const axiosError = error as AxiosError<ApiEnvelope<unknown>>;
  const envelope = axiosError.response?.data;

  return {
    message: envelope?.message ?? 'lockey_error_unexpected',
    meta: envelope?.meta,
    errors: envelope?.errors,
    status: axiosError.response?.status,
  };
}

