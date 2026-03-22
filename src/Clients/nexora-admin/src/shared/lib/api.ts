import axios, {
  type AxiosError,
  type AxiosInstance,
  type AxiosRequestConfig,
} from 'axios';

import type { ApiEnvelope } from '@/shared/types/api';
import { useAuthStore } from '@/shared/lib/stores/authStore';

let currentLanguage = 'en';

/** Called by i18n setup to keep Accept-Language in sync. */
export function setApiLanguage(lang: string): void {
  currentLanguage = lang;
  apiClient.defaults.headers.common['Accept-Language'] = lang;
}

function createApiClient(): AxiosInstance {
  const client = axios.create({
    baseURL: import.meta.env.VITE_API_BASE_URL ?? '/api/v1',
    headers: {
      'Content-Type': 'application/json',
      'Accept-Language': currentLanguage,
    },
  });

  client.interceptors.response.use(
    (response) => response,
    (error: AxiosError) => {
      if (error.response?.status === 401 && typeof window !== 'undefined') {
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

function unwrapEnvelope<T>(data: ApiEnvelope<T>, url: string): T {
  if (data.data === undefined) {
    throw new Error(`[api] Response envelope missing 'data' field for: ${url}`);
  }
  return data.data;
}

/** Typed API helpers that unwrap ApiEnvelope automatically. */
export const api = {
  async get<T>(url: string, params?: Record<string, unknown>): Promise<T> {
    const response = await apiClient.get<ApiEnvelope<T>>(url, { params });
    return unwrapEnvelope(response.data, url);
  },

  async post<T>(
    url: string,
    data?: unknown,
    config?: AxiosRequestConfig,
  ): Promise<T> {
    const response = await apiClient.post<ApiEnvelope<T>>(url, data, config);
    return unwrapEnvelope(response.data, url);
  },

  async put<T>(url: string, data?: unknown): Promise<T> {
    const response = await apiClient.put<ApiEnvelope<T>>(url, data);
    return unwrapEnvelope(response.data, url);
  },

  async delete<T = void>(url: string): Promise<T> {
    const response = await apiClient.delete<ApiEnvelope<T>>(url);
    return unwrapEnvelope(response.data, url);
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

