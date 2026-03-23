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
  versionKeys,
  useDocumentVersions,
  useAddDocumentVersion,
} from './useDocumentVersions';

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

describe('versionKeys', () => {
  it('list_WithDocumentId_IncludesDocumentIdAndVersions', () => {
    expect(versionKeys.list('d1')).toEqual([
      'documents',
      'documents',
      'detail',
      'd1',
      'versions',
    ]);
  });
});

describe('useDocumentVersions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with encoded documentId', async () => {
    const mockVersions = [{ id: 'v1', versionNumber: 1 }];
    mockApiGet.mockResolvedValue(mockVersions);

    const { result } = renderHook(() => useDocumentVersions('d1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/documents/documents/${encodeURIComponent('d1')}/versions`,
    );
  });

  it('should not fetch when documentId is empty', () => {
    const { result } = renderHook(() => useDocumentVersions(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});

describe('useAddDocumentVersion', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded documentId and payload', async () => {
    const version = { id: 'v2', versionNumber: 2 };
    mockApiPost.mockResolvedValue(version);

    const { result } = renderHook(() => useAddDocumentVersion('d1'), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').AddVersionRequest = { storageKey: 'key2', fileSize: 2048 };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/documents/documents/${encodeURIComponent('d1')}/versions`,
      payload,
    );
  });
});
