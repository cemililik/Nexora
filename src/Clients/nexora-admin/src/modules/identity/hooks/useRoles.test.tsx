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
  toast: { success: vi.fn(), error: vi.fn() },
}));

import { roleKeys, useRoles, usePermissions, useCreateRole } from './useRoles';

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

describe('roleKeys', () => {
  it('all_Called_ReturnsBaseKey', () => {
    expect(roleKeys.all).toEqual(['identity', 'roles']);
  });

  it('list_Called_ReturnsListKey', () => {
    expect(roleKeys.list()).toEqual(['identity', 'roles', 'list']);
  });

  it('permissions_WithModule_IncludesModule', () => {
    expect(roleKeys.permissions('contacts')).toEqual([
      'identity',
      'permissions',
      'contacts',
    ]);
  });

  it('permissions_WithoutModule_UsesAll', () => {
    expect(roleKeys.permissions()).toEqual(['identity', 'permissions', 'all']);
  });
});

describe('useRoles', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint', async () => {
    const mockRoles = [
      { id: '1', name: 'Admin', isSystemRole: true, isActive: true, permissions: [] },
    ];
    mockApiGet.mockResolvedValue(mockRoles);

    const { result } = renderHook(() => useRoles(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/identity/roles');
  });
});

describe('usePermissions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint and no params when module is undefined', async () => {
    const mockPerms = [{ id: '1', module: 'contacts', resource: 'contact', action: 'read', key: 'contacts.contact.read' }];
    mockApiGet.mockResolvedValue(mockPerms);

    const { result } = renderHook(() => usePermissions(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/identity/permissions', undefined);
  });

  it('should pass module filter when provided', async () => {
    mockApiGet.mockResolvedValue([]);

    const { result } = renderHook(() => usePermissions('contacts'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/identity/permissions', { module: 'contacts' });
  });
});

describe('useCreateRole', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const createdRole = { id: 'r1', name: 'Editor', isSystemRole: false, isActive: true, permissions: [] };
    mockApiPost.mockResolvedValue(createdRole);

    const { result } = renderHook(() => useCreateRole(), {
      wrapper: createWrapper(),
    });

    const payload = { name: 'Editor', description: 'Can edit content' };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/identity/roles', payload);
  });
});
