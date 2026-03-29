import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';

import { Button } from '@/shared/components/ui/button';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { usePagination } from '@/shared/hooks/usePagination';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { formatRelativeTime } from '@/shared/lib/date';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { useSignatures } from '../hooks/useSignatures';
import { SignatureRequestStatusBadge } from '../components/SignatureStatusBadge';
import type { SignatureRequestDto, SignatureRequestStatus } from '../types';

const STATUSES: SignatureRequestStatus[] = [
  'Draft', 'Sent', 'PartiallySigned', 'Completed', 'Cancelled', 'Expired',
];

const STATUS_KEY_MAP: Record<SignatureRequestStatus, string> = {
  Draft: 'lockey_documents_signatures_status_draft',
  Sent: 'lockey_documents_signatures_status_sent',
  PartiallySigned: 'lockey_documents_signatures_status_partially_signed',
  Completed: 'lockey_documents_signatures_status_completed',
  Cancelled: 'lockey_documents_signatures_status_cancelled',
  Expired: 'lockey_documents_signatures_status_expired',
};

export default function SignatureListPage() {
  const { t, i18n } = useTranslation('documents');
  const navigate = useNavigate();
  const { page, pageSize, setPage, setPageSize } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const canCreate = hasPermission('documents.signature.create');

  const [searchParams, setSearchParams] = useSearchParams();
  const rawStatus = searchParams.get('status');
  const status = STATUSES.includes(rawStatus as SignatureRequestStatus)
    ? (rawStatus as SignatureRequestStatus)
    : undefined;

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_documents_module_name' },
      { label: 'lockey_documents_signatures_title' },
    ]);
  }, [setBreadcrumbs]);

  const { data, isPending } = useSignatures({ page, pageSize, status });

  const columns: ColumnDef<SignatureRequestDto>[] = [
    {
      key: 'title',
      header: t('lockey_documents_signatures_col_title'),
      render: (row) => row.title,
    },
    {
      key: 'status',
      header: t('lockey_documents_signatures_col_status'),
      render: (row) => <SignatureRequestStatusBadge status={row.status} />,
    },
    {
      key: 'recipients',
      header: t('lockey_documents_signatures_col_recipients'),
      render: (row) => `${row.signedCount}/${row.recipientCount}`,
    },
    {
      key: 'expiresAt',
      header: t('lockey_documents_signatures_col_expires_at'),
      render: (row) =>
        row.expiresAt ? new Date(row.expiresAt).toLocaleDateString(i18n.language) : '-',
    },
    {
      key: 'createdAt',
      header: t('lockey_documents_signatures_col_created_at'),
      render: (row) => formatRelativeTime(row.createdAt),
    },
    {
      key: 'actions',
      header: t('lockey_documents_signatures_col_actions'),
      render: (row) => (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => navigate(`/documents/signatures/${row.id}`)}
        >
          {t('lockey_documents_action_edit')}
        </Button>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('lockey_documents_signatures_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_documents_signatures_description')}
          </p>
        </div>
        {canCreate && (
          <Button type="button" onClick={() => navigate('/documents/signatures/create')}>
            {t('lockey_documents_signatures_create')}
          </Button>
        )}
      </div>

      <div className="flex items-center gap-4">
        <Select
          value={status ?? '__all__'}
          onValueChange={(v) => {
            setSearchParams((prev: URLSearchParams) => {
              const next = new URLSearchParams(prev);
              if (v === '__all__') {
                next.delete('status');
              } else {
                next.set('status', v);
              }
              next.set('page', '1');
              return next;
            });
          }}
        >
          <SelectTrigger className="w-48" aria-label={t('lockey_documents_signatures_filter_status')}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t('lockey_documents_signatures_filter_all_statuses')}</SelectItem>
            {STATUSES.map((s) => (
              <SelectItem key={s} value={s}>
                {t(STATUS_KEY_MAP[s])}
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
        emptyMessage={t('lockey_documents_signatures_empty')}
        keyExtractor={(row) => row.id}
      />
    </div>
  );
}
