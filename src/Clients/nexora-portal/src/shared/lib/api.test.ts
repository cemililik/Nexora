import { describe, expect, it, vi, beforeEach } from 'vitest';

const { mockGet, mockPost, mockPut, mockDelete, mockHeaders } = vi.hoisted(() => ({
  mockGet: vi.fn(),
  mockPost: vi.fn(),
  mockPut: vi.fn(),
  mockDelete: vi.fn(),
  mockHeaders: {} as Record<string, string>,
}));

vi.mock('@/i18n/routing', () => ({
  routing: { locales: ['en', 'tr'], defaultLocale: 'en' },
}));

vi.mock('axios', async (importOriginal) => {
  const actual = await importOriginal<typeof import('axios')>();
  return {
    ...actual,
    default: {
      ...actual.default,
      create: () => ({
        get: mockGet,
        post: mockPost,
        put: mockPut,
        delete: mockDelete,
        defaults: { headers: { common: mockHeaders } },
        interceptors: {
          response: { use: vi.fn() },
          request: { use: vi.fn() },
        },
      }),
    },
  };
});

import axios from 'axios';
import { api, setAuthToken, extractApiError, apiClient } from './api';

describe('extractApiError', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should return lockey_error_unexpected for non-Axios errors', () => {
    const result = extractApiError(new Error('some error'));
    expect(result.message).toBe('lockey_error_unexpected');
    expect(result.status).toBeUndefined();
  });

  it('should return lockey_error_unexpected for null errors', () => {
    const result = extractApiError(null);
    expect(result.message).toBe('lockey_error_unexpected');
  });

  it('should return lockey_error_unexpected for string errors', () => {
    const result = extractApiError('something broke');
    expect(result.message).toBe('lockey_error_unexpected');
  });

  it('should extract message and status from Axios error with envelope', () => {
    const error = new axios.AxiosError(
      'Bad Request',
      '400',
      undefined,
      undefined,
      {
        status: 400,
        statusText: 'Bad Request',
        headers: {},
        config: { headers: new axios.AxiosHeaders() },
        data: {
          data: null,
          message: 'lockey_error_validation_failed',
          meta: { field: 'email' },
          errors: [{ key: 'lockey_validation_email_invalid' }],
        },
      },
    );

    const result = extractApiError(error);
    expect(result.message).toBe('lockey_error_validation_failed');
    expect(result.status).toBe(400);
    expect(result.meta).toEqual({ field: 'email' });
    expect(result.errors).toEqual([{ key: 'lockey_validation_email_invalid' }]);
  });

  it('should fallback to lockey_error_unexpected when Axios error has no envelope', () => {
    const error = new axios.AxiosError(
      'Internal Server Error',
      '500',
      undefined,
      undefined,
      {
        status: 500,
        statusText: 'Internal Server Error',
        headers: {},
        config: { headers: new axios.AxiosHeaders() },
        data: undefined,
      },
    );

    const result = extractApiError(error);
    expect(result.message).toBe('lockey_error_unexpected');
    expect(result.status).toBe(500);
  });

  it('should handle Axios error without response (network error)', () => {
    const error = new axios.AxiosError(
      'Network Error',
      'ERR_NETWORK',
    );

    const result = extractApiError(error);
    expect(result.message).toBe('lockey_error_unexpected');
    expect(result.status).toBeUndefined();
  });
});

describe('api.get', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should unwrap envelope data', async () => {
    mockGet.mockResolvedValue({
      data: { data: { id: 1, name: 'Test' }, message: null, meta: null, errors: null },
    });
    const result = await api.get<{ id: number; name: string }>('/test');
    expect(result).toEqual({ id: 1, name: 'Test' });
  });

  it('should pass params to axios', async () => {
    mockGet.mockResolvedValue({
      data: { data: [], message: null, meta: null, errors: null },
    });
    await api.get('/test', { page: 1 });
    expect(mockGet).toHaveBeenCalledWith('/test', { params: { page: 1 } });
  });

  it('should throw when data field is undefined', async () => {
    mockGet.mockResolvedValue({ data: { message: 'error' } });
    await expect(api.get('/test')).rejects.toThrow("missing 'data' field");
  });
});

describe('api.post', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should unwrap envelope data', async () => {
    mockPost.mockResolvedValue({
      data: { data: { id: 'new' }, message: null },
    });
    const result = await api.post<{ id: string }>('/test', { name: 'New' });
    expect(result).toEqual({ id: 'new' });
  });

  it('should propagate axios errors', async () => {
    mockPost.mockRejectedValue(new Error('Network Error'));
    await expect(api.post('/test', {})).rejects.toThrow('Network Error');
  });
});

describe('api.put', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should unwrap envelope data', async () => {
    mockPut.mockResolvedValue({
      data: { data: { updated: true }, message: null },
    });
    const result = await api.put<{ updated: boolean }>('/test', { name: 'Updated' });
    expect(result).toEqual({ updated: true });
  });
});

describe('api.delete', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should return undefined for 204 responses', async () => {
    mockDelete.mockResolvedValue({ status: 204, data: null });
    const result = await api.delete('/test/1');
    expect(result).toBeUndefined();
  });

  it('should unwrap envelope for non-204 responses', async () => {
    mockDelete.mockResolvedValue({
      status: 200,
      data: { data: { deleted: true }, message: null },
    });
    const result = await api.delete<{ deleted: boolean }>('/test/1');
    expect(result).toEqual({ deleted: true });
  });
});

describe('api.getRaw', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should return full envelope', async () => {
    const envelope = { data: [1, 2], message: 'lockey_success', meta: { total: 2 }, errors: null };
    mockGet.mockResolvedValue({ data: envelope });
    const result = await api.getRaw('/test');
    expect(result).toEqual(envelope);
  });
});

describe('setAuthToken', () => {
  beforeEach(() => {
    delete mockHeaders['Authorization'];
  });

  it('should set Bearer header when token provided', () => {
    setAuthToken('my-token');
    expect(apiClient.defaults.headers.common['Authorization']).toBe('Bearer my-token');
  });

  it('should remove header when null', () => {
    setAuthToken('my-token');
    setAuthToken(null);
    expect(apiClient.defaults.headers.common['Authorization']).toBeUndefined();
  });
});
