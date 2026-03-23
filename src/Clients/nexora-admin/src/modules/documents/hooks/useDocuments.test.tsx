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
  documentKeys,
  useDocuments,
  useDocument,
  useGenerateUploadUrl,
  useConfirmUpload,
  useUpdateDocumentMetadata,
  useArchiveDocument,
  useRestoreDocument,
  useMoveDocument,
  useLinkDocument,
  useUnlinkDocument,
} from './useDocuments';

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

describe('documentKeys', () => {
  it('all_Called_ReturnsBaseKey', () => {
    expect(documentKeys.all).toEqual(['documents', 'documents']);
  });

  it('list_WithParams_IncludesParams', () => {
    const params = { page: 1, pageSize: 10, folderId: 'f1' };
    expect(documentKeys.list(params)).toEqual([
      'documents',
      'documents',
      'list',
      params,
    ]);
  });

  it('detail_WithId_IncludesId', () => {
    expect(documentKeys.detail('d1')).toEqual([
      'documents',
      'documents',
      'detail',
      'd1',
    ]);
  });
});

describe('useDocuments', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint and params', async () => {
    const mockData = { items: [], totalCount: 0 };
    mockApiGet.mockResolvedValue(mockData);

    const params = { page: 1, pageSize: 20, folderId: 'f1', search: 'report', status: undefined };
    const { result } = renderHook(() => useDocuments(params), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/documents/documents', {
      page: 1,
      pageSize: 20,
      folderId: 'f1',
      search: 'report',
      status: undefined,
    });
  });
});

describe('useDocument', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with encoded id', async () => {
    const mockDoc = { id: 'd1', name: 'test.pdf' };
    mockApiGet.mockResolvedValue(mockDoc);

    const { result } = renderHook(() => useDocument('d1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/documents/documents/${encodeURIComponent('d1')}`,
    );
  });

  it('should not fetch when id is empty', () => {
    const { result } = renderHook(() => useDocument(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});

describe('useGenerateUploadUrl', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const urlDto = { url: 'https://minio/upload', storageKey: 'key1' };
    mockApiPost.mockResolvedValue(urlDto);

    const { result } = renderHook(() => useGenerateUploadUrl(), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').GenerateUploadUrlRequest = { fileName: 'test.pdf', contentType: 'application/pdf', fileSize: 1024 };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/documents/documents/upload-url', payload);
  });
});

describe('useConfirmUpload', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const doc = { id: 'd1', name: 'test.pdf' };
    mockApiPost.mockResolvedValue(doc);

    const { result } = renderHook(() => useConfirmUpload(), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').ConfirmUploadRequest = { folderId: 'f1', storageKey: 'key1', name: 'test.pdf', mimeType: 'application/pdf', fileSize: 1024 };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/documents/documents/confirm-upload', payload);
  });
});

describe('useUpdateDocumentMetadata', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.put with encoded id and payload', async () => {
    const updated = { id: 'd1', name: 'renamed.pdf' };
    mockApiPut.mockResolvedValue(updated);

    const { result } = renderHook(() => useUpdateDocumentMetadata('d1'), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').UpdateDocumentMetadataRequest = { name: 'renamed.pdf', description: 'Updated' };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPut).toHaveBeenCalledWith(
      `/documents/documents/${encodeURIComponent('d1')}`,
      payload,
    );
  });
});

describe('useArchiveDocument', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.delete with encoded id', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useArchiveDocument(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('d1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      `/documents/documents/${encodeURIComponent('d1')}`,
    );
  });
});

describe('useRestoreDocument', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded id and /restore suffix', async () => {
    mockApiPost.mockResolvedValue(undefined);

    const { result } = renderHook(() => useRestoreDocument(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('d1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/documents/documents/${encodeURIComponent('d1')}/restore`,
    );
  });
});

describe('useMoveDocument', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded id and move payload', async () => {
    const moved = { id: 'd1', folderId: 'f2' };
    mockApiPost.mockResolvedValue(moved);

    const { result } = renderHook(() => useMoveDocument('d1'), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').MoveDocumentRequest = { targetFolderId: 'f2' };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/documents/documents/${encodeURIComponent('d1')}/move`,
      payload,
    );
  });
});

describe('useLinkDocument', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded id and link payload', async () => {
    const linked = { id: 'd1' };
    mockApiPost.mockResolvedValue(linked);

    const { result } = renderHook(() => useLinkDocument('d1'), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').LinkDocumentRequest = { entityType: 'Contact', entityId: 'c1' };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/documents/documents/${encodeURIComponent('d1')}/link`,
      payload,
    );
  });
});

describe('useUnlinkDocument', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.delete with encoded id and /link suffix', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useUnlinkDocument('d1'), {
      wrapper: createWrapper(),
    });

    result.current.mutate();

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      `/documents/documents/${encodeURIComponent('d1')}/link`,
    );
  });
});
