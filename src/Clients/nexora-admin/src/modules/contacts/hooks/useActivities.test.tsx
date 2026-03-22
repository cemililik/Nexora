import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

// Mock API
const mockApiGet = vi.fn();
const mockApiPost = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockApiGet(...args),
    post: (...args: unknown[]) => mockApiPost(...args),
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() },
}));

import { activityKeys, useActivities, useLogActivity } from './useActivities';

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

describe('activityKeys', () => {
  it('all_WithContactId_ReturnsCorrectKey', () => {
    expect(activityKeys.all('c-1')).toEqual([
      'contacts',
      'activities',
      'c-1',
    ]);
  });

  it('all_WithDifferentContactId_ReturnsCorrectKey', () => {
    expect(activityKeys.all('c-55')).toEqual([
      'contacts',
      'activities',
      'c-55',
    ]);
  });
});

describe('useActivities', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint', async () => {
    const mockActivities = [
      { id: 'a-1', type: 'Call', description: 'Phone call', timestamp: '2026-03-22T10:00:00Z' },
    ];
    mockApiGet.mockResolvedValue(mockActivities);

    const { result } = renderHook(() => useActivities('contact-1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('contact-1')}/activities`,
    );
  });

  it('should not fetch when contactId is empty', () => {
    const { result } = renderHook(() => useActivities(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });

  it('should encode contactId in URL', async () => {
    mockApiGet.mockResolvedValue([]);

    const { result } = renderHook(() => useActivities('id/special'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('id/special')}/activities`,
    );
  });
});

describe('useLogActivity', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const created = { id: 'a-2', type: 'Meeting', description: 'Client meeting' };
    mockApiPost.mockResolvedValue(created);

    const { result } = renderHook(() => useLogActivity('contact-1'), {
      wrapper: createWrapper(),
    });

    const payload = { type: 'Meeting', description: 'Client meeting', occurredAt: '2026-03-22T14:00:00Z' };
    result.current.mutate(payload as never);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('contact-1')}/activities`,
      payload,
    );
  });
});
