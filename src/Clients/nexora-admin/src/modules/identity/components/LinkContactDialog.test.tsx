import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';

const mockApiGet = vi.fn();
const mockApiPost = vi.fn();

vi.mock('@/shared/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockApiGet(...args),
    post: (...args: unknown[]) => mockApiPost(...args),
    delete: vi.fn(),
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key, i18n: { language: 'en' } }),
}));

vi.mock('@/shared/hooks/useApiError', () => ({
  useApiError: () => ({ handleApiError: vi.fn() }),
}));

import { LinkContactDialog } from './LinkContactDialog';

function renderWithClient(node: ReactNode) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>{node}</QueryClientProvider>,
  );
}

describe('LinkContactDialog', () => {
  beforeEach(() => {
    mockApiGet.mockReset();
    mockApiPost.mockReset();
    mockApiGet.mockResolvedValue({
      items: [
        { id: 'c1', displayName: 'Alice', email: 'alice@test.com' },
        { id: 'c2', displayName: 'Bob', email: 'bob@test.com' },
      ],
      totalCount: 2,
      page: 1,
      pageSize: 20,
    });
  });

  it('renders title and search placeholder', async () => {
    renderWithClient(
      <LinkContactDialog userId="u1" open onOpenChange={vi.fn()} />,
    );

    expect(
      await screen.findByText('lockey_identity_user_link_contact_dialog_title'),
    ).toBeInTheDocument();
    expect(
      screen.getByPlaceholderText('lockey_identity_user_link_contact_search_placeholder'),
    ).toBeInTheDocument();
  });

  it('lists search results and disables confirm until selection', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <LinkContactDialog userId="u1" open onOpenChange={vi.fn()} />,
    );

    await waitFor(() => expect(screen.getByText('Alice')).toBeInTheDocument());
    const confirmButton = screen.getByRole('button', {
      name: 'lockey_identity_user_link_contact_confirm',
    });
    expect(confirmButton).toBeDisabled();

    await user.click(screen.getByText('Alice'));
    expect(confirmButton).toBeEnabled();
  });

  it('calls post with contactId on confirm and closes dialog', async () => {
    mockApiPost.mockResolvedValue(undefined);
    const onOpenChange = vi.fn();
    const user = userEvent.setup();

    renderWithClient(
      <LinkContactDialog userId="u1" open onOpenChange={onOpenChange} />,
    );

    await waitFor(() => expect(screen.getByText('Bob')).toBeInTheDocument());
    await user.click(screen.getByText('Bob'));
    await user.click(
      screen.getByRole('button', { name: 'lockey_identity_user_link_contact_confirm' }),
    );

    await waitFor(() => {
      expect(mockApiPost).toHaveBeenCalledWith(
        '/identity/users/u1/link-contact',
        { contactId: 'c2' },
      );
    });
    await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false));
  });

  it('cancel closes dialog without posting', async () => {
    const onOpenChange = vi.fn();
    const user = userEvent.setup();

    renderWithClient(
      <LinkContactDialog userId="u1" open onOpenChange={onOpenChange} />,
    );

    await user.click(
      screen.getByRole('button', { name: 'lockey_identity_user_link_contact_cancel' }),
    );

    expect(mockApiPost).not.toHaveBeenCalled();
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});
