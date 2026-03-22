import { describe, expect, it, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode, createElement } from 'react';

const mockApiGet = vi.fn();
const mockApiPost = vi.fn();
vi.mock('@/shared/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockApiGet(...args),
    post: (...args: unknown[]) => mockApiPost(...args),
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

import { useRoles, usePermissions, useCreateRole } from './useRoles';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return createElement(QueryClientProvider, { client: queryClient }, children);
  };
}

describe('useRoles', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should fetch all roles', async () => {
    const mockRoles = [
      { id: 'r1', name: 'Admin', isSystemRole: true, isActive: true, permissions: ['identity.user.read'] },
    ];
    mockApiGet.mockResolvedValue(mockRoles);

    const { result } = renderHook(() => useRoles(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.data).toEqual(mockRoles);
    });
  });

  it('should fetch permissions with optional module filter', async () => {
    const mockPerms = [
      { id: 'p1', module: 'identity', resource: 'user', action: 'read', key: 'identity.user.read' },
    ];
    mockApiGet.mockResolvedValue(mockPerms);

    const { result } = renderHook(() => usePermissions('identity'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.data).toEqual(mockPerms);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/identity/permissions', { module: 'identity' });
  });

  it('should create role and invalidate queries', async () => {
    const newRole = { id: 'r2', name: 'Editor', isSystemRole: false, isActive: true, permissions: [] };
    mockApiPost.mockResolvedValue(newRole);

    const { result } = renderHook(() => useCreateRole(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ name: 'Editor', description: 'Can edit content' });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });
  });
});
