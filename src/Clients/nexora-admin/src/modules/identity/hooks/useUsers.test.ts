import { describe, expect, it, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode, createElement } from 'react';

const mockApiGet = vi.fn();
const mockApiPost = vi.fn();
const mockApiPut = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockApiGet(...args),
    post: (...args: unknown[]) => mockApiPost(...args),
    put: (...args: unknown[]) => mockApiPut(...args),
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

import { useUsers, useUser, useCreateUser } from './useUsers';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return createElement(QueryClientProvider, { client: queryClient }, children);
  };
}

describe('useUsers', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should fetch paginated users', async () => {
    const mockData = {
      items: [{ id: 'u1', email: 'a@b.com', firstName: 'A', lastName: 'B', status: 'Active' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    };
    mockApiGet.mockResolvedValue(mockData);

    const { result } = renderHook(() => useUsers({ page: 1, pageSize: 20 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.data).toEqual(mockData);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/identity/users', { page: 1, pageSize: 20 });
  });

  it('should fetch single user by id', async () => {
    const mockUser = { id: 'u1', email: 'a@b.com', firstName: 'A', lastName: 'B', status: 'Active', organizations: [] };
    mockApiGet.mockResolvedValue(mockUser);

    const { result } = renderHook(() => useUser('u1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.data).toEqual(mockUser);
    });
  });

  it('should not fetch user when id is empty', () => {
    renderHook(() => useUser(''), {
      wrapper: createWrapper(),
    });

    expect(mockApiGet).not.toHaveBeenCalled();
  });

  it('should create user and invalidate queries', async () => {
    const newUser = { id: 'u2', email: 'new@b.com', firstName: 'New', lastName: 'User', status: 'Active' };
    mockApiPost.mockResolvedValue(newUser);

    const { result } = renderHook(() => useCreateUser(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({
      email: 'new@b.com',
      firstName: 'New',
      lastName: 'User',
      temporaryPassword: 'pass1234',
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/identity/users', {
      email: 'new@b.com',
      firstName: 'New',
      lastName: 'User',
      temporaryPassword: 'pass1234',
    });
  });
});
