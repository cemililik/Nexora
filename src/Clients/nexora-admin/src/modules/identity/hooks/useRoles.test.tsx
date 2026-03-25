import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

// Mock API
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

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock('@/shared/hooks/useApiError', () => ({
  useApiError: () => ({ handleApiError: vi.fn() }),
}));

import { roleKeys, useRoles, useRole, usePermissions, useCreateRole, useUpdateRole, useDeleteRole } from './useRoles';

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
  it('should return base key when all is accessed', () => {
    expect(roleKeys.all).toEqual(['identity', 'roles']);
  });

  it('should return list key', () => {
    expect(roleKeys.list()).toEqual(['identity', 'roles', 'list']);
  });

  it('should include module in permissions key when module is provided', () => {
    expect(roleKeys.permissions('contacts')).toEqual([
      'identity',
      'permissions',
      'contacts',
    ]);
  });

  it('should use all when module is not provided', () => {
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

describe('useRole', () => {
  beforeEach(() => { vi.clearAllMocks(); });

  it('should call api.get with encoded role id', async () => {
    mockApiGet.mockResolvedValue({ id: 'r1', name: 'Admin' });
    const { result } = renderHook(() => useRole('r1'), { wrapper: createWrapper() });
    await waitFor(() => { expect(result.current.isSuccess).toBe(true); });
    expect(mockApiGet).toHaveBeenCalledWith('/identity/roles/r1');
  });

  it('should be disabled when id is empty', () => {
    const { result } = renderHook(() => useRole(''), { wrapper: createWrapper() });
    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});

describe('useUpdateRole', () => {
  beforeEach(() => { vi.clearAllMocks(); });

  it('should call api.put with correct endpoint and payload', async () => {
    const updatedRole = { id: 'r1', name: 'Updated', isSystemRole: false, isActive: true, permissions: [] };
    mockApiPut.mockResolvedValue(updatedRole);
    const { result } = renderHook(() => useUpdateRole(), { wrapper: createWrapper() });
    const payload = { id: 'r1', name: 'Updated', description: 'Updated desc', permissionIds: [] };
    result.current.mutate(payload);
    await waitFor(() => { expect(result.current.isSuccess).toBe(true); });
    expect(mockApiPut).toHaveBeenCalledWith('/identity/roles/r1', { name: 'Updated', description: 'Updated desc', permissionIds: [] });
  });
});

describe('useDeleteRole', () => {
  beforeEach(() => { vi.clearAllMocks(); });

  it('should call api.delete with encoded role id', async () => {
    mockApiDelete.mockResolvedValue(undefined);
    const { result } = renderHook(() => useDeleteRole(), { wrapper: createWrapper() });
    result.current.mutate('r1');
    await waitFor(() => { expect(result.current.isSuccess).toBe(true); });
    expect(mockApiDelete).toHaveBeenCalledWith('/identity/roles/r1');
  });
});
