import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

// Mock API
const mockApiGet = vi.fn();
const mockApiPut = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockApiGet(...args),
    put: (...args: unknown[]) => mockApiPut(...args),
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() },
}));

import {
  prefKeys,
  useCommunicationPreferences,
  useUpdatePreferences,
} from './useCommunicationPreferences';

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

describe('prefKeys', () => {
  it('all_WithContactId_ReturnsCorrectKey', () => {
    expect(prefKeys.all('c-1')).toEqual(['contacts', 'preferences', 'c-1']);
  });

  it('all_WithDifferentContactId_ReturnsCorrectKey', () => {
    expect(prefKeys.all('c-42')).toEqual(['contacts', 'preferences', 'c-42']);
  });
});

describe('useCommunicationPreferences', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint', async () => {
    const mockPrefs = [{ id: 'p-1', channel: 'Email', enabled: true }];
    mockApiGet.mockResolvedValue(mockPrefs);

    const { result } = renderHook(
      () => useCommunicationPreferences('contact-1'),
      { wrapper: createWrapper() },
    );

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('contact-1')}/preferences`,
    );
  });

  it('should not fetch when contactId is empty', () => {
    const { result } = renderHook(
      () => useCommunicationPreferences(''),
      { wrapper: createWrapper() },
    );

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });

  it('should encode contactId in URL', async () => {
    mockApiGet.mockResolvedValue([]);

    const { result } = renderHook(
      () => useCommunicationPreferences('id/special'),
      { wrapper: createWrapper() },
    );

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('id/special')}/preferences`,
    );
  });
});

describe('useUpdatePreferences', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.put with correct endpoint and payload', async () => {
    const updated = [{ id: 'p-1', channel: 'Email', enabled: false }];
    mockApiPut.mockResolvedValue(updated);

    const { result } = renderHook(
      () => useUpdatePreferences('contact-1'),
      { wrapper: createWrapper() },
    );

    const payload = { preferences: [{ channel: 'Email', enabled: false }] };
    result.current.mutate(payload as never);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPut).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('contact-1')}/preferences`,
      payload,
    );
  });
});
