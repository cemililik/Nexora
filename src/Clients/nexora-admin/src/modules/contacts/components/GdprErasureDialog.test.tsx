import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

const mockMutate = vi.fn();
const mockUseGdprErasure = vi.fn();

vi.mock('../hooks/useGdprErasure', () => ({
  useGdprErasure: (contactId: string) => mockUseGdprErasure(contactId),
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() },
}));

import { GdprErasureDialog } from './GdprErasureDialog';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
  };
}

describe('GdprErasureDialog', () => {
  beforeEach(() => {
    mockMutate.mockReset();
    mockUseGdprErasure.mockReset();
    mockUseGdprErasure.mockReturnValue({
      mutate: mockMutate,
      isPending: false,
    });
  });

  it('disables the destructive submit when no reason and no name match', () => {
    const Wrapper = createWrapper();
    render(
      <Wrapper>
        <GdprErasureDialog
          contactId="c-1"
          contactDisplayName="Ada Lovelace"
          open
          onOpenChange={vi.fn()}
        />
      </Wrapper>,
    );

    const submit = screen.getByRole('button', { name: 'gdpr_erasure_confirm_action' });
    expect(submit).toBeDisabled();
  });

  it('keeps submit disabled when reason is valid but name mismatches', async () => {
    const user = userEvent.setup();
    const Wrapper = createWrapper();
    render(
      <Wrapper>
        <GdprErasureDialog
          contactId="c-1"
          contactDisplayName="Ada Lovelace"
          open
          onOpenChange={vi.fn()}
        />
      </Wrapper>,
    );

    await user.type(
      screen.getByLabelText(/gdpr_erasure_reason_label/),
      'Valid legal basis for erasure',
    );
    await user.type(
      screen.getByLabelText(/gdpr_erasure_confirm_label/),
      'Wrong Name',
    );

    const submit = screen.getByRole('button', { name: 'gdpr_erasure_confirm_action' });
    await waitFor(() => expect(submit).toBeDisabled());
    expect(screen.getByText('gdpr_erasure_name_mismatch')).toBeInTheDocument();
    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('enables submit and calls the mutation when reason and name both match', async () => {
    const user = userEvent.setup();
    const Wrapper = createWrapper();
    render(
      <Wrapper>
        <GdprErasureDialog
          contactId="c-1"
          contactDisplayName="Ada Lovelace"
          open
          onOpenChange={vi.fn()}
        />
      </Wrapper>,
    );

    await user.type(
      screen.getByLabelText(/gdpr_erasure_reason_label/),
      'Data subject request received',
    );
    await user.type(
      screen.getByLabelText(/gdpr_erasure_confirm_label/),
      'Ada Lovelace',
    );

    const submit = screen.getByRole('button', { name: 'gdpr_erasure_confirm_action' });
    await waitFor(() => expect(submit).toBeEnabled());

    await user.click(submit);

    await waitFor(() => expect(mockMutate).toHaveBeenCalledTimes(1));
    const [payload] = mockMutate.mock.calls[0] as [{ reason: string }, unknown];
    expect(payload.reason).toBe('Data subject request received');
  });

  it('rejects reasons shorter than the minimum length', async () => {
    const user = userEvent.setup();
    const Wrapper = createWrapper();
    render(
      <Wrapper>
        <GdprErasureDialog
          contactId="c-1"
          contactDisplayName="Ada Lovelace"
          open
          onOpenChange={vi.fn()}
        />
      </Wrapper>,
    );

    await user.type(screen.getByLabelText(/gdpr_erasure_reason_label/), 'short');
    await user.type(
      screen.getByLabelText(/gdpr_erasure_confirm_label/),
      'Ada Lovelace',
    );

    const submit = screen.getByRole('button', { name: 'gdpr_erasure_confirm_action' });
    await waitFor(() => expect(submit).toBeDisabled());
    expect(mockMutate).not.toHaveBeenCalled();
  });
});
