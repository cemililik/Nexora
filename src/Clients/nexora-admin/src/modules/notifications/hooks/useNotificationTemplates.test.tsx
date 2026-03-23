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
  useNotificationTemplates,
  useNotificationTemplate,
  useCreateNotificationTemplate,
  useUpdateNotificationTemplate,
  useDeleteNotificationTemplate,
  useAddTemplateTranslation,
} from './useNotificationTemplates';

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
    expect(templateKeys.all).toEqual(['notifications', 'templates']);
  });

  it('list_WithParams_IncludesParams', () => {
    const params = { page: 1, pageSize: 10, channel: 'Email' as const };
    expect(templateKeys.list(params)).toEqual([
      'notifications',
      'templates',
      'list',
      params,
    ]);
  });

  it('detail_WithId_IncludesId', () => {
    expect(templateKeys.detail('t1')).toEqual([
      'notifications',
      'templates',
      'detail',
      't1',
    ]);
  });
});

describe('useNotificationTemplates', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint and params', async () => {
    const mockData = { items: [], totalCount: 0 };
    mockApiGet.mockResolvedValue(mockData);

    const params = { page: 1, pageSize: 20, channel: undefined, module: undefined, isActive: undefined };
    const { result } = renderHook(() => useNotificationTemplates(params), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/notifications/templates', {
      page: 1,
      pageSize: 20,
      channel: undefined,
      module: undefined,
      isActive: undefined,
    });
  });
});

describe('useNotificationTemplate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with encoded id', async () => {
    const mockTemplate = { id: 't1', code: 'welcome' };
    mockApiGet.mockResolvedValue(mockTemplate);

    const { result } = renderHook(() => useNotificationTemplate('t1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/notifications/templates/${encodeURIComponent('t1')}`,
    );
  });

  it('should not fetch when id is empty', () => {
    const { result } = renderHook(() => useNotificationTemplate(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });

  it('should not fetch when id is create', () => {
    const { result } = renderHook(() => useNotificationTemplate('create'), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockApiGet).not.toHaveBeenCalled();
  });
});

describe('useCreateNotificationTemplate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const created = { id: 't1', code: 'welcome' };
    mockApiPost.mockResolvedValue(created);

    const { result } = renderHook(() => useCreateNotificationTemplate(), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').CreateNotificationTemplateRequest = { code: 'welcome', module: 'identity', channel: 'Email', subject: 'Welcome', body: 'Hello', format: 'Html' };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/notifications/templates', payload);
  });
});

describe('useUpdateNotificationTemplate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.put with encoded id and payload', async () => {
    const updated = { id: 't1', subject: 'Updated' };
    mockApiPut.mockResolvedValue(updated);

    const { result } = renderHook(() => useUpdateNotificationTemplate('t1'), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').UpdateNotificationTemplateRequest = { subject: 'Updated', body: 'New body', format: 'Html' };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPut).toHaveBeenCalledWith(
      `/notifications/templates/${encodeURIComponent('t1')}`,
      payload,
    );
  });
});

describe('useDeleteNotificationTemplate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.delete with encoded id', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useDeleteNotificationTemplate(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('t1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      `/notifications/templates/${encodeURIComponent('t1')}`,
    );
  });
});

describe('useAddTemplateTranslation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded templateId and payload', async () => {
    const translation = { id: 'tr1', languageCode: 'tr' };
    mockApiPost.mockResolvedValue(translation);

    const { result } = renderHook(() => useAddTemplateTranslation('t1'), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').AddTemplateTranslationRequest = { languageCode: 'tr', subject: 'Hoşgeldiniz', body: 'Merhaba' };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/notifications/templates/${encodeURIComponent('t1')}/translations`,
      payload,
    );
  });
});
