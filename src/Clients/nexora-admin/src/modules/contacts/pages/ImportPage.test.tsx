import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

const generateUploadUrlMutateAsync = vi.fn();
const previewMutateAsync = vi.fn();
const validateMutateAsync = vi.fn();
const confirmMutateAsync = vi.fn();

const mockGenerateUploadUrl = vi.fn();
const mockPreview = vi.fn();
const mockValidate = vi.fn();
const mockConfirm = vi.fn();
const mockImportStatus = vi.fn();

vi.mock('../hooks/useImportExport', () => ({
  useGenerateImportUploadUrl: () => mockGenerateUploadUrl(),
  usePreviewImport: () => mockPreview(),
  useValidateImport: () => mockValidate(),
  useConfirmImport: () => mockConfirm(),
  useImportStatus: (jobId: string) => mockImportStatus(jobId),
}));

vi.mock('@/shared/hooks/useApiError', () => ({
  useApiError: () => ({ handleApiError: vi.fn() }),
}));

vi.mock('@/shared/hooks/useUnsavedChangesGuard', () => ({
  useUnsavedChangesGuard: () => {},
}));

vi.mock('@/shared/lib/stores/uiStore', () => ({
  useUiStore: (selector: (s: unknown) => unknown) => {
    const state = { setBreadcrumbs: vi.fn() };
    return selector(state);
  },
}));

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key: string, opts?: Record<string, unknown>) =>
      opts && 'count' in opts ? `${key}:${opts.count}` : key,
    i18n: { language: 'en' },
  }),
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() },
}));

// Stub global fetch (PUT to MinIO).
const fetchSpy = vi.fn().mockResolvedValue({ ok: true, status: 200 });
Object.defineProperty(globalThis, 'fetch', { value: fetchSpy, writable: true });

import ImportPage from './ImportPage';

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

function setupHooks() {
  mockGenerateUploadUrl.mockReturnValue({
    mutateAsync: generateUploadUrlMutateAsync,
    isPending: false,
  });
  mockPreview.mockReturnValue({
    mutateAsync: previewMutateAsync,
    isPending: false,
  });
  mockValidate.mockReturnValue({
    mutateAsync: validateMutateAsync,
    isPending: false,
  });
  mockConfirm.mockReturnValue({
    mutateAsync: confirmMutateAsync,
    isPending: false,
  });
  mockImportStatus.mockReturnValue({ data: undefined });
}

describe('ImportPage wizard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    fetchSpy.mockResolvedValue({ ok: true, status: 200 });
    setupHooks();
  });

  it('renders the upload step initially', () => {
    const Wrapper = createWrapper();
    render(<ImportPage />, { wrapper: Wrapper });

    expect(
      screen.getByText('lockey_contacts_import_wizard_upload_and_preview'),
    ).toBeInTheDocument();
  });

  it('advances from upload to mapping step after file upload + preview', async () => {
    const user = userEvent.setup();

    generateUploadUrlMutateAsync.mockResolvedValue({
      uploadUrl: 'https://minio.test/bucket/key',
      storageKey: 'org1/contacts/imports/file.csv',
      expiresAt: '2026-04-24T00:00:00Z',
    });
    previewMutateAsync.mockResolvedValue({
      headers: ['fname', 'email_address'],
      rows: [{ fname: 'Alice', email_address: 'a@example.com' }],
      totalRowCount: 42,
    });

    const Wrapper = createWrapper();
    render(<ImportPage />, { wrapper: Wrapper });

    const file = new File(['content'], 'contacts.csv', { type: 'text/csv' });
    const input = document.querySelector<HTMLInputElement>('input[type="file"]');
    expect(input).not.toBeNull();
    await user.upload(input!, file);

    await user.click(
      screen.getByText('lockey_contacts_import_wizard_upload_and_preview'),
    );

    await waitFor(() => {
      expect(
        screen.getByText('lockey_contacts_import_mapping_title'),
      ).toBeInTheDocument();
    });
    // fname/email_address appear twice: once in the mapping table source cell
    // and once as preview-row column header, so tolerate duplicates.
    expect(screen.getAllByText('fname').length).toBeGreaterThan(0);
    expect(screen.getAllByText('email_address').length).toBeGreaterThan(0);
  });

  it('calls validate then confirm with the submitted mapping', async () => {
    const user = userEvent.setup();

    generateUploadUrlMutateAsync.mockResolvedValue({
      uploadUrl: 'https://minio.test/bucket/key',
      storageKey: 'org1/contacts/imports/file.csv',
      expiresAt: '2026-04-24T00:00:00Z',
    });
    // Header named exactly "email" → identity mapping will map it to email automatically.
    previewMutateAsync.mockResolvedValue({
      headers: ['email'],
      rows: [{ email: 'a@example.com' }],
      totalRowCount: 1,
    });
    validateMutateAsync.mockResolvedValue({
      totalRows: 1,
      errorCount: 0,
      errors: [],
    });
    confirmMutateAsync.mockResolvedValue({
      jobId: '00000000-0000-0000-0000-000000000001',
      status: 'Pending',
      totalRows: 1,
      processedRows: 0,
      successCount: 0,
      errorCount: 0,
    });

    const Wrapper = createWrapper();
    render(<ImportPage />, { wrapper: Wrapper });

    const file = new File(['content'], 'contacts.csv', { type: 'text/csv' });
    await user.upload(
      document.querySelector<HTMLInputElement>('input[type="file"]')!,
      file,
    );
    await user.click(
      screen.getByText('lockey_contacts_import_wizard_upload_and_preview'),
    );

    await waitFor(() =>
      expect(
        screen.getByText('lockey_contacts_import_mapping_title'),
      ).toBeInTheDocument(),
    );

    // Identity mapping for "email" → email. Click Validate.
    await user.click(screen.getByText('lockey_contacts_import_wizard_next'));

    await waitFor(() => {
      expect(validateMutateAsync).toHaveBeenCalledWith({
        storageKey: 'org1/contacts/imports/file.csv',
        fileFormat: 'csv',
        columnMapping: { email: 'email' },
      });
    });

    await waitFor(() =>
      expect(
        screen.getByText('lockey_contacts_import_validation_no_errors'),
      ).toBeInTheDocument(),
    );

    await user.click(
      screen.getByText('lockey_contacts_import_wizard_start_import'),
    );

    await waitFor(() => {
      expect(confirmMutateAsync).toHaveBeenCalledWith({
        fileName: 'contacts.csv',
        fileFormat: 'csv',
        storageKey: 'org1/contacts/imports/file.csv',
        columnMapping: { email: 'email' },
      });
    });
  });

  it('shows the "proceed anyway" button when validation has errors', async () => {
    const user = userEvent.setup();

    generateUploadUrlMutateAsync.mockResolvedValue({
      uploadUrl: 'https://minio.test/bucket/key',
      storageKey: 'org1/contacts/imports/file.csv',
      expiresAt: '2026-04-24T00:00:00Z',
    });
    previewMutateAsync.mockResolvedValue({
      headers: ['email'],
      rows: [],
      totalRowCount: 5,
    });
    validateMutateAsync.mockResolvedValue({
      totalRows: 5,
      errorCount: 2,
      errors: [
        {
          rowNumber: 3,
          errorKey: 'lockey_contacts_import_validation_email_invalid',
          fieldName: 'email',
        },
        {
          rowNumber: 4,
          errorKey: 'lockey_contacts_import_validation_email_required',
          fieldName: 'email',
        },
      ],
    });

    const Wrapper = createWrapper();
    render(<ImportPage />, { wrapper: Wrapper });

    const file = new File(['content'], 'contacts.csv', { type: 'text/csv' });
    await user.upload(
      document.querySelector<HTMLInputElement>('input[type="file"]')!,
      file,
    );
    await user.click(
      screen.getByText('lockey_contacts_import_wizard_upload_and_preview'),
    );
    await waitFor(() =>
      expect(
        screen.getByText('lockey_contacts_import_mapping_title'),
      ).toBeInTheDocument(),
    );
    await user.click(screen.getByText('lockey_contacts_import_wizard_next'));

    await waitFor(() =>
      expect(
        screen.getByText('lockey_contacts_import_validation_proceed_anyway'),
      ).toBeInTheDocument(),
    );
    expect(
      screen.getByText('lockey_contacts_import_validation_back_to_mapping'),
    ).toBeInTheDocument();
  });
});
