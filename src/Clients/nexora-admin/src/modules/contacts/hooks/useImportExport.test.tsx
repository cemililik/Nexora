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
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() },
}));

import {
  importKeys,
  useGenerateImportUploadUrl,
  useConfirmImport,
  useImportStatus,
  useStartExport,
  useGdprExport,
  useGdprDelete,
} from './useImportExport';

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

describe('importKeys', () => {
  it('job_WithJobId_ReturnsCorrectKey', () => {
    expect(importKeys.job('j-1')).toEqual(['contacts', 'import', 'j-1']);
  });

  it('job_WithDifferentJobId_ReturnsCorrectKey', () => {
    expect(importKeys.job('j-42')).toEqual(['contacts', 'import', 'j-42']);
  });
});

describe('useGenerateImportUploadUrl', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const uploadUrl = { url: 'https://storage.example.com/upload', jobId: 'j-1' };
    mockApiPost.mockResolvedValue(uploadUrl);

    const { result } = renderHook(() => useGenerateImportUploadUrl(), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').GenerateImportUploadUrlRequest = {
      fileName: 'contacts.csv',
      contentType: 'text/csv',
      fileSize: 1024,
    };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      '/contacts/contacts/import/upload-url',
      payload,
    );
  });
});

describe('useConfirmImport', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const importJob = { id: 'j-1', status: 'Pending' };
    mockApiPost.mockResolvedValue(importJob);

    const { result } = renderHook(() => useConfirmImport(), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').ConfirmImportRequest = {
      fileName: 'contacts.csv',
      fileFormat: 'csv',
      storageKey: 'org-1/contacts/imports/abc/contacts.csv',
    };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      '/contacts/contacts/import',
      payload,
    );
  });
});

describe('useImportStatus', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint', async () => {
    const jobStatus = { id: 'j-1', status: 'Completed', processedCount: 100 };
    mockApiGet.mockResolvedValue(jobStatus);

    const { result } = renderHook(() => useImportStatus('j-1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/import/${encodeURIComponent('j-1')}`,
    );
  });

  it('should not fetch when jobId is empty', () => {
    const { result } = renderHook(() => useImportStatus(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});

describe('useStartExport', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const exportJob = { id: 'ej-1', status: 'Pending' };
    mockApiPost.mockResolvedValue(exportJob);

    const { result } = renderHook(() => useStartExport(), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').StartExportRequest = {
      format: 'csv',
    };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      '/contacts/contacts/export',
      payload,
    );
  });
});

describe('useGdprExport', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint', async () => {
    mockApiPost.mockResolvedValue(undefined);

    const { result } = renderHook(() => useGdprExport('contact-1'), {
      wrapper: createWrapper(),
    });

    result.current.mutate();

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('contact-1')}/gdpr/export`,
    );
  });
});

describe('useGdprDelete', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    mockApiPost.mockResolvedValue(undefined);

    const { result } = renderHook(() => useGdprDelete('contact-1'), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').GdprDeleteRequest = {
      reason: 'User requested data deletion',
    };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('contact-1')}/gdpr/delete`,
      payload,
    );
  });
});
