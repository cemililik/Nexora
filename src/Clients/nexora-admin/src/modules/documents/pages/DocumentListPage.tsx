import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import { FileText, Trash2 } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { SearchInput } from '@/shared/components/data/SearchInput';
import { EmptyState } from '@/shared/components/feedback/EmptyState';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import { usePagination } from '@/shared/hooks/usePagination';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { useApiError } from '@/shared/hooks/useApiError';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { formatRelativeTime } from '@/shared/lib/date';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { useDocuments, useArchiveDocument } from '../hooks/useDocuments';
import { useFolders } from '../hooks/useFolders';
import { DocumentStatusBadge } from '../components/DocumentStatusBadge';
import { FileSize } from '../components/FileSize';
import type { DocumentDto, DocumentStatus } from '../types';

const STATUSES: DocumentStatus[] = ['Active', 'Archived', 'Deleted', 'PendingRender'];

const STATUS_KEY_MAP: Record<DocumentStatus, string> = {
  Active: 'lockey_documents_status_active',
  Archived: 'lockey_documents_status_archived',
  Deleted: 'lockey_documents_status_deleted',
  PendingRender: 'lockey_documents_status_pending_render',
};

export default function DocumentListPage() {
  const { t } = useTranslation('documents');
  const navigate = useNavigate();
  const { page, pageSize, setPage, setPageSize } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const { handleApiError } = useApiError();

  const [searchParams, setSearchParams] = useSearchParams();
  const search = searchParams.get('search') ?? '';
  const rawStatus = searchParams.get('status');
  const status = STATUSES.includes(rawStatus as DocumentStatus)
    ? (rawStatus as DocumentStatus)
    : undefined;
  const folderId = searchParams.get('folderId') ?? undefined;

  const [deleteId, setDeleteId] = useState<string | null>(null);

  const updateFilter = (key: string, value: string) => {
    setSearchParams((prev: URLSearchParams) => {
      const next = new URLSearchParams(prev);
      if (value) {
        next.set(key, value);
      } else {
        next.delete(key);
      }
      next.set('page', '1');
      return next;
    });
  };

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_documents_module_name' },
      { label: 'lockey_documents_list_title' },
    ]);
  }, [setBreadcrumbs]);

  const { data, isPending } = useDocuments({
    page,
    pageSize,
    search: search || undefined,
    status,
    folderId,
  });

  const { data: folders } = useFolders();
  const archiveDoc = useArchiveDocument();

  const hasActiveFilters = !!(search || status || folderId);

  const emptyStateNode = useMemo(() => {
    if (hasActiveFilters) {
      return (
        <EmptyState
          icon={FileText}
          title={t('lockey_common_no_results_filtered', { ns: 'common' })}
        />
      );
    }
    return (
      <EmptyState
        icon={FileText}
        title={t('lockey_documents_empty_title')}
        description={t('lockey_documents_empty_description')}
        action={{ label: t('lockey_documents_empty_upload'), onClick: () => navigate('/documents/documents/upload') }}
      />
    );
  }, [hasActiveFilters, t, navigate]);

  const columns: ColumnDef<DocumentDto>[] = [
    {
      key: 'name',
      header: t('lockey_documents_col_name'),
      render: (row) => row.name,
    },
    {
      key: 'mimeType',
      header: t('lockey_documents_col_mime_type'),
      render: (row) => row.mimeType,
    },
    {
      key: 'fileSize',
      header: t('lockey_documents_col_file_size'),
      render: (row) => <FileSize bytes={row.fileSize} />,
    },
    {
      key: 'status',
      header: t('lockey_documents_col_status'),
      render: (row) => <DocumentStatusBadge status={row.status} />,
    },
    {
      key: 'version',
      header: t('lockey_documents_col_version'),
      render: (row) => t('lockey_documents_version_prefix', { version: row.currentVersion }),
    },
    {
      key: 'createdAt',
      header: t('lockey_documents_col_created_at'),
      render: (row) => formatRelativeTime(row.createdAt),
    },
    {
      key: 'actions',
      header: t('lockey_documents_col_actions'),
      render: (row) => (
        <div className="flex items-center gap-1">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={(e) => {
              e.stopPropagation();
              navigate(`/documents/documents/${row.id}`);
            }}
          >
            {t('lockey_documents_action_edit')}
          </Button>
          {hasPermission('documents.document.delete') && (
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-destructive hover:text-destructive"
              onClick={(e) => {
                e.stopPropagation();
                setDeleteId(row.id);
              }}
              aria-label={t('lockey_documents_action_delete')}
            >
              <Trash2 className="h-4 w-4" />
            </Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('lockey_documents_list_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_documents_list_description')}
          </p>
        </div>
        {hasPermission('documents.document.upload') && (
          <Button type="button" onClick={() => navigate('/documents/documents/upload')}>
            {t('lockey_documents_action_upload')}
          </Button>
        )}
      </div>

      <div className="flex items-center gap-4">
        <SearchInput
          value={search}
          onChange={(v) => updateFilter('search', v)}
          placeholder={t('lockey_documents_list_search')}
        />
        <Select
          value={status ?? '__all__'}
          onValueChange={(v) => updateFilter('status', v === '__all__' ? '' : v)}
        >
          <SelectTrigger className="w-48" aria-label={t('lockey_documents_filter_status')}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t('lockey_documents_filter_all_statuses')}</SelectItem>
            {STATUSES.map((s) => (
              <SelectItem key={s} value={s}>
                {t(STATUS_KEY_MAP[s])}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select
          value={folderId ?? '__all__'}
          onValueChange={(v) => updateFilter('folderId', v === '__all__' ? '' : v)}
        >
          <SelectTrigger className="w-48" aria-label={t('lockey_documents_filter_folder')}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t('lockey_documents_filter_all_folders')}</SelectItem>
            {folders?.map((f) => (
              <SelectItem key={f.id} value={f.id}>
                {f.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        totalCount={data?.totalCount ?? 0}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        onPageSizeChange={setPageSize}
        isLoading={isPending}
        emptyState={emptyStateNode}
        onRowClick={(row) => navigate(`/documents/documents/${row.id}`)}
        keyExtractor={(row) => row.id}
      />

      <ConfirmDialog
        open={deleteId !== null}
        onOpenChange={() => setDeleteId(null)}
        title={t('lockey_documents_confirm_delete_title')}
        description={t('lockey_documents_confirm_delete')}
        variant="destructive"
        onConfirm={() => {
          if (deleteId) {
            archiveDoc.mutate(deleteId, {
              onSuccess: () => setDeleteId(null),
              onError: (error) => {
                handleApiError(error);
                setDeleteId(null);
              },
            });
          }
        }}
        isPending={archiveDoc.isPending}
      />
    </div>
  );
}
