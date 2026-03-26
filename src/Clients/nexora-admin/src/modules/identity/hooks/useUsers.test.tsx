import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

// Mock API
const mockApiGet = vi.fn();
const mockApiPut = vi.fn();
const mockApiDelete = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockApiGet(...args),
    put: (...args: unknown[]) => mockApiPut(...args),
    delete: (...args: unknown[]) => mockApiDelete(...args),
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock('@/shared/hooks/useApiError', () => ({
  useApiError: () => ({ handleApiError: vi.fn() }),
}));

import { userKeys, useUsers, useUser, useDeleteUser, useUserRoles, useAssignUserRoles } from './useUsers';

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
  it('should return base key when all is accessed', () => {
    expect(userKeys.all).toEqual(['identity', 'users']);
  });

  it('should include pagination params in list key', () => {
    const params = { page: 2, pageSize: 25 };
    expect(userKeys.list(params)).toEqual(['identity', 'users', 'list', params]);
  });

  it('should include user ID in detail key', () => {
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

describe('useDeleteUser', () => {
  beforeEach(() => { vi.clearAllMocks(); });

  it('should call api.delete with encoded user id', async () => {
    mockApiDelete.mockResolvedValue(undefined);
    const { result } = renderHook(() => useDeleteUser(), { wrapper: createWrapper() });
    result.current.mutate('user-42');
    await waitFor(() => { expect(result.current.isSuccess).toBe(true); });
    expect(mockApiDelete).toHaveBeenCalledWith('/identity/users/user-42');
  });
});

describe('useUserRoles', () => {
  beforeEach(() => { vi.clearAllMocks(); });

  it('should call api.get with userId and organizationId params', async () => {
    mockApiGet.mockResolvedValue([{ id: 'r1', name: 'Admin' }]);
    const { result } = renderHook(() => useUserRoles('user-1', 'org-1'), { wrapper: createWrapper() });
    await waitFor(() => { expect(result.current.isSuccess).toBe(true); });
    expect(mockApiGet).toHaveBeenCalledWith('/identity/users/user-1/roles', { organizationId: 'org-1' });
  });

  it('should be disabled when userId is empty', () => {
    const { result } = renderHook(() => useUserRoles('', 'org-1'), { wrapper: createWrapper() });
    expect(result.current.fetchStatus).toBe('idle');
  });

  it('should be disabled when organizationId is empty', () => {
    const { result } = renderHook(() => useUserRoles('user-1', ''), { wrapper: createWrapper() });
    expect(result.current.fetchStatus).toBe('idle');
  });
});

describe('useAssignUserRoles', () => {
  beforeEach(() => { vi.clearAllMocks(); });

  it('should call api.put with correct endpoint and payload', async () => {
    mockApiPut.mockResolvedValue(undefined);
    const { result } = renderHook(() => useAssignUserRoles('user-1'), { wrapper: createWrapper() });
    const payload = { organizationId: 'org-1', roleIds: ['r1', 'r2'] };
    result.current.mutate(payload);
    await waitFor(() => { expect(result.current.isSuccess).toBe(true); });
    expect(mockApiPut).toHaveBeenCalledWith('/identity/users/user-1/roles', payload);
  });
});
