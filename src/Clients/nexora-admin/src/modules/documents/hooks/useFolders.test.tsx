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
  folderKeys,
  useFolders,
  useFolder,
  useCreateFolder,
  useRenameFolder,
  useDeleteFolder,
} from './useFolders';

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

describe('folderKeys', () => {
  it('all_Called_ReturnsBaseKey', () => {
    expect(folderKeys.all).toEqual(['documents', 'folders']);
  });

  it('list_WithParentId_IncludesParentId', () => {
    expect(folderKeys.list('f1')).toEqual([
      'documents',
      'folders',
      'list',
      'f1',
    ]);
  });

  it('list_WithoutParentId_UsesRoot', () => {
    expect(folderKeys.list()).toEqual([
      'documents',
      'folders',
      'list',
      'root',
    ]);
  });

  it('detail_WithId_IncludesId', () => {
    expect(folderKeys.detail('f1')).toEqual([
      'documents',
      'folders',
      'detail',
      'f1',
    ]);
  });
});

describe('useFolders', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint and parentFolderId', async () => {
    const mockFolders = [{ id: 'f1', name: 'Reports' }];
    mockApiGet.mockResolvedValue(mockFolders);

    const { result } = renderHook(() => useFolders('parent1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/documents/folders', {
      parentFolderId: 'parent1',
    });
  });

  it('should call api.get with undefined parentFolderId for root', async () => {
    const mockFolders = [{ id: 'f1', name: 'Root Folder' }];
    mockApiGet.mockResolvedValue(mockFolders);

    const { result } = renderHook(() => useFolders(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/documents/folders', {
      parentFolderId: undefined,
    });
  });
});

describe('useFolder', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with encoded id', async () => {
    const mockFolder = { id: 'f1', name: 'Reports' };
    mockApiGet.mockResolvedValue(mockFolder);

    const { result } = renderHook(() => useFolder('f1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/documents/folders/${encodeURIComponent('f1')}`,
    );
  });

  it('should not fetch when id is empty', () => {
    const { result } = renderHook(() => useFolder(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});

describe('useCreateFolder', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const created = { id: 'f2', name: 'New Folder' };
    mockApiPost.mockResolvedValue(created);

    const { result } = renderHook(() => useCreateFolder(), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').CreateFolderRequest = { name: 'New Folder', parentFolderId: 'f1' };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/documents/folders', payload);
  });
});

describe('useRenameFolder', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.put with encoded id and payload', async () => {
    const renamed = { id: 'f1', name: 'Renamed' };
    mockApiPut.mockResolvedValue(renamed);

    const { result } = renderHook(() => useRenameFolder('f1'), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').RenameFolderRequest = { newName: 'Renamed' };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPut).toHaveBeenCalledWith(
      `/documents/folders/${encodeURIComponent('f1')}`,
      payload,
    );
  });
});

describe('useDeleteFolder', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.delete with encoded id', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useDeleteFolder(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('f1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      `/documents/folders/${encodeURIComponent('f1')}`,
    );
  });
});
