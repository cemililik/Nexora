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
  notificationKeys,
  useNotifications,
  useNotification,
  useSendNotification,
  useSendBulkNotification,
} from './useNotifications';

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

describe('notificationKeys', () => {
  it('all_Called_ReturnsBaseKey', () => {
    expect(notificationKeys.all).toEqual(['notifications', 'notifications']);
  });

  it('list_WithParams_IncludesParams', () => {
    const params = { page: 1, pageSize: 10, channel: 'Email' as const };
    expect(notificationKeys.list(params)).toEqual([
      'notifications',
      'notifications',
      'list',
      params,
    ]);
  });

  it('detail_WithId_IncludesId', () => {
    expect(notificationKeys.detail('n1')).toEqual([
      'notifications',
      'notifications',
      'detail',
      'n1',
    ]);
  });
});

describe('useNotifications', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint and params', async () => {
    const mockData = { items: [], totalCount: 0 };
    mockApiGet.mockResolvedValue(mockData);

    const params = { page: 1, pageSize: 20, channel: undefined, status: undefined };
    const { result } = renderHook(() => useNotifications(params), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/notifications', {
      page: 1,
      pageSize: 20,
      channel: undefined,
      status: undefined,
    });
  });
});

describe('useNotification', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with encoded id', async () => {
    const mockNotif = { id: 'n1', subject: 'Welcome' };
    mockApiGet.mockResolvedValue(mockNotif);

    const { result } = renderHook(() => useNotification('n1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/notifications/${encodeURIComponent('n1')}`,
    );
  });

  it('should not fetch when id is empty', () => {
    const { result } = renderHook(() => useNotification(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});

describe('useSendNotification', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const sent = { id: 'n1', subject: 'Welcome' };
    mockApiPost.mockResolvedValue(sent);

    const { result } = renderHook(() => useSendNotification(), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').SendNotificationRequest = {
      channel: 'Email',
      contactId: 'c1',
      recipientAddress: 'test@example.com',
      subject: 'Welcome',
      body: 'Hello!',
    };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/notifications/send', payload);
  });
});

describe('useSendBulkNotification', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const bulkResult = { queuedCount: 5, skippedCount: 1 };
    mockApiPost.mockResolvedValue(bulkResult);

    const { result } = renderHook(() => useSendBulkNotification(), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').SendBulkNotificationRequest = {
      channel: 'Email',
      templateCode: 'welcome',
      recipients: [{ contactId: 'c1', address: 'test@example.com' }],
    };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/notifications/bulk', payload);
  });
});
