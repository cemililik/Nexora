import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

const mockApiGet = vi.fn();
const mockApiPost = vi.fn();
const mockApiDelete = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockApiGet(...args),
    post: (...args: unknown[]) => mockApiPost(...args),
    delete: (...args: unknown[]) => mockApiDelete(...args),
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

import {
  scheduleKeys,
  useScheduledNotifications,
  useScheduleNotification,
  useCancelScheduledNotification,
} from './useSchedule';

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

describe('scheduleKeys', () => {
  it('all_Called_ReturnsBaseKey', () => {
    expect(scheduleKeys.all).toEqual(['notifications', 'schedule']);
  });

  it('list_WithParams_IncludesParams', () => {
    const params = { page: 1, pageSize: 10 };
    expect(scheduleKeys.list(params)).toEqual([
      'notifications',
      'schedule',
      'list',
      params,
    ]);
  });
});

describe('useScheduledNotifications', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint and params', async () => {
    const mockData = { items: [], totalCount: 0 };
    mockApiGet.mockResolvedValue(mockData);

    const params = { page: 1, pageSize: 20 };
    const { result } = renderHook(() => useScheduledNotifications(params), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/notifications/schedule', {
      page: 1,
      pageSize: 20,
    });
  });
});

describe('useScheduleNotification', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const scheduled = { id: 'sch1', status: 'Pending' };
    mockApiPost.mockResolvedValue(scheduled);

    const { result } = renderHook(() => useScheduleNotification(), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').ScheduleNotificationRequest = {
      channel: 'Email',
      contactId: 'c1',
      recipientAddress: 'test@example.com',
      templateCode: 'welcome',
      scheduledAt: '2026-04-01T10:00:00Z',
    };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/notifications/schedule', payload);
  });
});

describe('useCancelScheduledNotification', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.delete with encoded id', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useCancelScheduledNotification(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('sch1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      `/notifications/schedule/${encodeURIComponent('sch1')}`,
    );
  });
});
