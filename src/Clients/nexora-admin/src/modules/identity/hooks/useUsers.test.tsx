import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

// Mock API
const mockApiGet = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: { get: (...args: unknown[]) => mockApiGet(...args) },
}));

import { userKeys, useUsers, useUser } from './useUsers';

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

describe('userKeys', () => {
  it('all_Called_ReturnsBaseKey', () => {
    expect(userKeys.all).toEqual(['identity', 'users']);
  });

  it('list_WithParams_IncludesParams', () => {
    const params = { page: 2, pageSize: 25 };
    expect(userKeys.list(params)).toEqual(['identity', 'users', 'list', params]);
  });

  it('detail_WithId_IncludesId', () => {
    expect(userKeys.detail('user-123')).toEqual([
      'identity',
      'users',
      'detail',
      'user-123',
    ]);
  });
});

describe('useUsers', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct URL and params', async () => {
    const mockData = {
      items: [{ id: '1', email: 'test@example.com' }],
      totalCount: 1,
    };
    mockApiGet.mockResolvedValue(mockData);

    const params = { page: 1, pageSize: 10 };
    const { result } = renderHook(() => useUsers(params), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/identity/users', {
      page: 1,
      pageSize: 10,
    });
  });

  it('should return paginated data', async () => {
    const mockData = {
      items: [
        { id: '1', email: 'alice@example.com' },
        { id: '2', email: 'bob@example.com' },
      ],
      totalCount: 2,
    };
    mockApiGet.mockResolvedValue(mockData);

    const { result } = renderHook(() => useUsers({ page: 1, pageSize: 10 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.data).toEqual(mockData);
    });
  });
});

describe('useUser', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct URL including userId', async () => {
    const mockUser = { id: 'user-42', email: 'test@example.com' };
    mockApiGet.mockResolvedValue(mockUser);

    const { result } = renderHook(() => useUser('user-42'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/identity/users/user-42');
  });

  it('should be disabled when id is empty', () => {
    const { result } = renderHook(() => useUser(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});
