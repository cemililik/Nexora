import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

const startExportMutate = vi.fn();
const mockUseStartExport = vi.fn();
const mockUseExportStatus = vi.fn();

vi.mock('../hooks/useImportExport', () => ({
  useStartExport: () => mockUseStartExport(),
  useExportStatus: (jobId: string) => mockUseExportStatus(jobId),
}));

vi.mock('@/shared/hooks/useApiError', () => ({
  useApiError: () => ({ handleApiError: vi.fn() }),
}));

vi.mock('@/shared/hooks/useUnsavedChangesGuard', () => ({
  useUnsavedChangesGuard: () => ({
    blocker: { state: 'unblocked' },
    isBlocked: false,
    proceed: vi.fn(),
    reset: vi.fn(),
  }),
}));

vi.mock('@/shared/lib/stores/uiStore', () => ({
  useUiStore: (selector: (s: unknown) => unknown) => {
    const state = { setBreadcrumbs: vi.fn() };
    return selector(state);
  },
}));

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key: string) => key,
    i18n: { language: 'en' },
  }),
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() },
}));

import ExportPage from './ExportPage';

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

function setup(overrides?: { exportStatus?: unknown }) {
  mockUseStartExport.mockReturnValue({
    mutate: startExportMutate,
    isPending: false,
  });
  mockUseExportStatus.mockReturnValue(
    overrides?.exportStatus ?? { data: undefined },
  );
}

describe('ExportPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setup();
  });

  it('renders the format select and submit button initially', () => {
    const Wrapper = createWrapper();
    render(<ExportPage />, { wrapper: Wrapper });

    expect(
      screen.getByText('lockey_contacts_export_form_format'),
    ).toBeInTheDocument();
    expect(
      screen.getByText('lockey_contacts_export_button_submit'),
    ).toBeInTheDocument();
  });

  it('submits with only format when no optional inputs are set', async () => {
    const user = userEvent.setup();
    const Wrapper = createWrapper();
    render(<ExportPage />, { wrapper: Wrapper });

    await user.click(screen.getByText('lockey_contacts_export_button_submit'));

    expect(startExportMutate).toHaveBeenCalledTimes(1);
    const [body] = startExportMutate.mock.calls[0]!;
    expect(body).toEqual({ format: 'csv' });
  });

  it('passes fields and date range in the mutation body when provided', async () => {
    const user = userEvent.setup();
    const Wrapper = createWrapper();
    render(<ExportPage />, { wrapper: Wrapper });

    // Toggle one field checkbox via its label.
    await user.click(
      screen.getByText('lockey_contacts_export_field_email'),
    );

    // Set dateFrom and dateTo via inputs.
    const dateInputs = document.querySelectorAll<HTMLInputElement>('input[type="date"]');
    expect(dateInputs.length).toBe(2);
    await user.type(dateInputs[0]!, '2026-01-01');
    await user.type(dateInputs[1]!, '2026-01-31');

    await user.click(screen.getByText('lockey_contacts_export_button_submit'));

    expect(startExportMutate).toHaveBeenCalledTimes(1);
    const [body] = startExportMutate.mock.calls[0]!;
    expect(body.format).toBe('csv');
    expect(body.fields).toEqual(['email']);
    expect(body.dateField).toBe('CreatedAt');
    expect(typeof body.dateFrom).toBe('string');
    expect(typeof body.dateTo).toBe('string');
    expect(body.dateFrom?.startsWith('2026-01-01')).toBe(true);
  });

  it('after submit shows status panel with job status and download link when completed', async () => {
    const user = userEvent.setup();
    startExportMutate.mockImplementation((_body, opts) => {
      opts?.onSuccess?.({
        jobId: '11111111-1111-1111-1111-111111111111',
        status: 'Pending',
        format: 'csv',
        totalRows: 0,
        createdAt: '2026-04-22T10:00:00Z',
      });
    });
    mockUseExportStatus.mockReturnValue({
      data: {
        jobId: '11111111-1111-1111-1111-111111111111',
        status: 'Completed',
        format: 'csv',
        totalRows: 42,
        createdAt: '2026-04-22T10:00:00Z',
        completedAt: '2026-04-22T10:05:00Z',
        downloadUrl: 'https://minio.test/export/file.csv',
      },
    });

    const Wrapper = createWrapper();
    render(<ExportPage />, { wrapper: Wrapper });

    await user.click(screen.getByText('lockey_contacts_export_button_submit'));

    await waitFor(() =>
      expect(
        screen.getByText('lockey_contacts_export_status_title'),
      ).toBeInTheDocument(),
    );
    expect(
      screen.getByText('lockey_contacts_export_status_completed'),
    ).toBeInTheDocument();

    // The download control is a Button that triggers a programmatic anchor click with
    // the `download` attribute (avoids popup-blocker suppression after async polling —
    // see T-019 review). Verify the button renders, then spy on document.createElement
    // to confirm the anchor is built with the right href + download when clicked.
    const downloadButton = screen.getByText(
      'lockey_contacts_export_button_download',
    ) as HTMLElement;
    expect(downloadButton).toBeInTheDocument();

    let capturedHref: string | null = null;
    let capturedDownload: string | null = null;
    const originalCreate = document.createElement.bind(document);
    const createSpy = vi
      .spyOn(document, 'createElement')
      .mockImplementation((tagName: string, options?: ElementCreationOptions) => {
        const element = originalCreate(tagName, options) as HTMLElement;
        if (tagName === 'a') {
          // Capture on set so we see the final values the component wrote.
          Object.defineProperty(element, 'href', {
            set(v: string) { capturedHref = v; },
            get() { return capturedHref ?? ''; },
            configurable: true,
          });
          Object.defineProperty(element, 'download', {
            set(v: string) { capturedDownload = v; },
            get() { return capturedDownload ?? ''; },
            configurable: true,
          });
          // Stub click so the test environment doesn't actually try to navigate.
          element.click = () => {};
        }
        return element;
      });

    try {
      await user.click(downloadButton);
      expect(capturedHref).toBe('https://minio.test/export/file.csv');
      expect(capturedDownload).toBe('');
    } finally {
      // Always restore the global stub even when an assertion throws — otherwise
      // subsequent tests inherit the spy and behave unpredictably.
      createSpy.mockRestore();
    }
  });
});
