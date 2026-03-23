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
  accessKeys,
  useDocumentAccess,
  useGrantDocumentAccess,
  useRevokeDocumentAccess,
} from './useDocumentAccess';

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

describe('accessKeys', () => {
  it('list_WithDocumentId_IncludesDocumentIdAndAccess', () => {
    expect(accessKeys.list('d1')).toEqual([
      'documents',
      'documents',
      'detail',
      'd1',
      'access',
    ]);
  });
});

describe('useDocumentAccess', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with encoded documentId', async () => {
    const mockAccess = [{ id: 'a1', userId: 'u1', permission: 'View' }];
    mockApiGet.mockResolvedValue(mockAccess);

    const { result } = renderHook(() => useDocumentAccess('d1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/documents/documents/${encodeURIComponent('d1')}/access`,
    );
  });

  it('should not fetch when documentId is empty', () => {
    const { result } = renderHook(() => useDocumentAccess(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});

describe('useGrantDocumentAccess', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded documentId and payload', async () => {
    const granted = { id: 'a1', userId: 'u1', permission: 'View' };
    mockApiPost.mockResolvedValue(granted);

    const { result } = renderHook(() => useGrantDocumentAccess('d1'), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').GrantAccessRequest = { userId: 'u1', permission: 'View' };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/documents/documents/${encodeURIComponent('d1')}/access`,
      payload,
    );
  });
});

describe('useRevokeDocumentAccess', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.delete with encoded documentId and accessId', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useRevokeDocumentAccess('d1'), {
      wrapper: createWrapper(),
    });

    result.current.mutate('a1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      `/documents/documents/${encodeURIComponent('d1')}/access/${encodeURIComponent('a1')}`,
    );
  });
});
