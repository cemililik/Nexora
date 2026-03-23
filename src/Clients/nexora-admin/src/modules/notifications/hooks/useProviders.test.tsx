import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

const mockApiGet = vi.fn();
const mockApiPost = vi.fn();
const mockApiPut = vi.fn();
const mockApiDelete = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockApiGet(...args),
    post: (...args: unknown[]) => mockApiPost(...args),
    put: (...args: unknown[]) => mockApiPut(...args),
    delete: (...args: unknown[]) => mockApiDelete(...args),
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

import {
  providerKeys,
  useProviders,
  useCreateProvider,
  useUpdateProvider,
  useTestProvider,
} from './useProviders';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        {children}
      </QueryClientProvider>
    );
  };
}

describe('providerKeys', () => {
  it('all_Called_ReturnsBaseKey', () => {
    expect(providerKeys.all).toEqual(['notifications', 'providers']);
  });

  it('list_WithChannel_IncludesChannel', () => {
    expect(providerKeys.list('Email')).toEqual([
      'notifications',
      'providers',
      'list',
      'Email',
    ]);
  });

  it('list_WithoutChannel_IncludesUndefined', () => {
    expect(providerKeys.list()).toEqual([
      'notifications',
      'providers',
      'list',
      undefined,
    ]);
  });
});

describe('useProviders', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint and channel', async () => {
    const mockProviders = [{ id: 'p1', providerName: 'SendGrid' }];
    mockApiGet.mockResolvedValue(mockProviders);

    const { result } = renderHook(() => useProviders('Email'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/notifications/providers', {
      channel: 'Email',
    });
  });

  it('should call api.get with undefined channel for all providers', async () => {
    const mockProviders = [{ id: 'p1' }];
    mockApiGet.mockResolvedValue(mockProviders);

    const { result } = renderHook(() => useProviders(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/notifications/providers', {
      channel: undefined,
    });
  });
});

describe('useCreateProvider', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const created = { id: 'p1', providerName: 'SendGrid' };
    mockApiPost.mockResolvedValue(created);

    const { result } = renderHook(() => useCreateProvider(), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').CreateNotificationProviderRequest = { channel: 'Email', providerName: 'SendGrid', config: '{}', dailyLimit: 1000, isDefault: true };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/notifications/providers', payload);
  });
});

describe('useUpdateProvider', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.put with encoded id and payload', async () => {
    const updated = { id: 'p1', dailyLimit: 2000 };
    mockApiPut.mockResolvedValue(updated);

    const { result } = renderHook(() => useUpdateProvider('p1'), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').UpdateNotificationProviderRequest = { config: '{"apiKey":"new"}', dailyLimit: 2000, isDefault: false };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPut).toHaveBeenCalledWith(
      `/notifications/providers/${encodeURIComponent('p1')}`,
      payload,
    );
  });
});

describe('useTestProvider', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded id and test payload', async () => {
    mockApiPost.mockResolvedValue(undefined);

    const { result } = renderHook(() => useTestProvider(), {
      wrapper: createWrapper(),
    });

    const input = { id: 'p1', data: { testRecipient: 'test@example.com' } };
    result.current.mutate(input);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/notifications/providers/${encodeURIComponent('p1')}/test`,
      { testRecipient: 'test@example.com' },
    );
  });
});
