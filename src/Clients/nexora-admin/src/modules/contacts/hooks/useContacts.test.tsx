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

import {
  contactKeys,
  useContacts,
  useContact,
  useContact360,
  useCreateContact,
  useUpdateContact,
  useArchiveContact,
  useRestoreContact,
} from './useContacts';

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

describe('contactKeys', () => {
  it('all_Called_ReturnsBaseKey', () => {
    expect(contactKeys.all).toEqual(['contacts', 'contacts']);
  });

  it('list_WithParams_IncludesParams', () => {
    const params = { page: 1, pageSize: 10, search: 'test' };
    expect(contactKeys.list(params)).toEqual([
      'contacts',
      'contacts',
      'list',
      params,
    ]);
  });

  it('detail_WithId_IncludesId', () => {
    expect(contactKeys.detail('c1')).toEqual([
      'contacts',
      'contacts',
      'detail',
      'c1',
    ]);
  });

  it('view360_WithId_IncludesId', () => {
    expect(contactKeys.view360('c1')).toEqual([
      'contacts',
      'contacts',
      '360',
      'c1',
    ]);
  });
});

describe('useContacts', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with correct endpoint and params', async () => {
    const mockData = { items: [], totalCount: 0 };
    mockApiGet.mockResolvedValue(mockData);

    const params = { page: 1, pageSize: 20, search: 'acme', status: undefined, type: undefined };
    const { result } = renderHook(() => useContacts(params), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith('/contacts/contacts', {
      page: 1,
      pageSize: 20,
      search: 'acme',
      status: undefined,
      type: undefined,
    });
  });
});

describe('useContact', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with encoded id', async () => {
    const mockContact = { id: 'c1', firstName: 'John' };
    mockApiGet.mockResolvedValue(mockContact);

    const { result } = renderHook(() => useContact('c1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}`,
    );
  });
});

describe('useContact360', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.get with encoded id and /360 suffix', async () => {
    const mockData = { contact: {}, tags: [], addresses: [] };
    mockApiGet.mockResolvedValue(mockData);

    const { result } = renderHook(() => useContact360('c1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiGet).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/360`,
    );
  });
});

describe('useCreateContact', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with correct endpoint and payload', async () => {
    const created = { id: 'c1', firstName: 'Jane' };
    mockApiPost.mockResolvedValue(created);

    const { result } = renderHook(() => useCreateContact(), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').CreateContactRequest = {
      type: 'Individual',
      firstName: 'Jane',
      lastName: 'Doe',
      source: 'Manual',
    };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith('/contacts/contacts', payload);
  });
});

describe('useUpdateContact', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.put with encoded id and payload', async () => {
    const updated = { id: 'c1', firstName: 'Jane' };
    mockApiPut.mockResolvedValue(updated);

    const { result } = renderHook(() => useUpdateContact('c1'), {
      wrapper: createWrapper(),
    });

    const payload: import('../types').UpdateContactRequest = {
      firstName: 'Janet',
      language: 'en',
      currency: 'USD',
    };
    result.current.mutate(payload);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPut).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}`,
      payload,
    );
  });
});

describe('useArchiveContact', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.delete with encoded id', async () => {
    mockApiDelete.mockResolvedValue(undefined);

    const { result } = renderHook(() => useArchiveContact(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('c1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiDelete).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}`,
    );
  });
});

describe('useRestoreContact', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call api.post with encoded id and /restore suffix', async () => {
    mockApiPost.mockResolvedValue(undefined);

    const { result } = renderHook(() => useRestoreContact(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('c1');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockApiPost).toHaveBeenCalledWith(
      `/contacts/contacts/${encodeURIComponent('c1')}/restore`,
    );
  });
});
