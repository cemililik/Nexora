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
  templateKeys,
  useTemplates,
  useTemplate,
  useCreateTemplate,
  useUpdateTemplate,
  useActivateTemplate,
  useDeactivateTemplate,
  useRenderTemplate,
} from './useTemplates';

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

describe('templateKeys', () => {
  it('all_Called_ReturnsBaseKey', () => {
    expect(templateKeys.all).toEqual(['documents', 'templates']);
  });

  it('list_WithParams_IncludesParams', () => {
    const params = { page: 1, pageSize: 10, category: 'Contract' as const };
    expect(templateKeys.list(params)).toEqual([
      'documents',
      'templates',
      'list',
      params,
    ]);
  });

  it('detail_WithId_IncludesId', () => {
    expect(templateKeys.detail('t1')).toEqual([
      'documents',
      'templates',
      'detail',
      't1',
    ]);
  });
});

describe('useTemplates', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint and params', async () => {
    const mockData = { items: [], totalCount: 0 };
    mockApiGet.mockResolvedValue(mockData);

    const params = { page: 1, pageSize: 20, category: undefined, isActive: true };
    const { result } = renderHook(() => useTemplates(params), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/documents/templates', {
      page: 1,
      pageSize: 20,
      category: undefined,
      isActive: true,
    });
  });
});

describe('useTemplate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with encoded id', async () => {
    const mockTemplate = { id: 'template/1', name: 'Invoice Template' };
    mockApiGet.mockResolvedValue(mockTemplate);

    const { result } = renderHook(() => useTemplate('template/1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/documents/templates/${encodeURIComponent('template/1')}`,
    );
  });

  it('should not fetch when id is empty', () => {
    const { result } = renderHook(() => useTemplate(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });

  it('should not fetch when id is create', () => {
    const { result } = renderHook(() => useTemplate('create'), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});

describe('useCreateTemplate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const created = { id: 't1', name: 'New Template' };
    mockApiPost.mockResolvedValue(created);

    const { result } = renderHook(() => useCreateTemplate(), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').CreateDocumentTemplateRequest = { name: 'New Template', category: 'Contract', format: 'Html', templateStorageKey: 'tpl-key-1' };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/documents/templates', payload);
  });
});

describe('useUpdateTemplate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.put with encoded id and payload', async () => {
    const updated = { id: 'tmpl 1', name: 'Updated' };
    mockApiPut.mockResolvedValue(updated);

    const { result } = renderHook(() => useUpdateTemplate('tmpl 1'), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').UpdateDocumentTemplateRequest = { name: 'Updated', category: 'Contract', format: 'Html' };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPut).toHaveBeenCalledWith(
      `/documents/templates/${encodeURIComponent('tmpl 1')}`,
      payload,
    );
  });
});

describe('useActivateTemplate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded id and /activate suffix', async () => {
    mockApiPost.mockResolvedValue(undefined);

    const { result } = renderHook(() => useActivateTemplate(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('template/1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/documents/templates/${encodeURIComponent('template/1')}/activate`,
    );
  });
});

describe('useDeactivateTemplate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded id and /deactivate suffix', async () => {
    mockApiPost.mockResolvedValue(undefined);

    const { result } = renderHook(() => useDeactivateTemplate(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('tmpl 1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/documents/templates/${encodeURIComponent('tmpl 1')}/deactivate`,
    );
  });
});

describe('useRenderTemplate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded id and render payload', async () => {
    const rendered = { documentId: 'd1', content: '<p>Rendered</p>' };
    mockApiPost.mockResolvedValue(rendered);

    const { result } = renderHook(() => useRenderTemplate('template/1'), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').RenderTemplateRequest = { folderId: 'f1', outputName: 'rendered-doc', variables: { name: 'John' } };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/documents/templates/${encodeURIComponent('template/1')}/render`,
      payload,
    );
  });
});
