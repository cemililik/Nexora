import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { CalendarClock } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { EmptyState } from '@/shared/components/feedback/EmptyState';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import { usePagination } from '@/shared/hooks/usePagination';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { formatRelativeTime } from '@/shared/lib/date';
import { useApiError } from '@/shared/hooks/useApiError';
import { useScheduledNotifications, useCancelScheduledNotification } from '../hooks/useSchedule';
import { ScheduleStatusBadge } from '../components/ScheduleStatusBadge';
import type { NotificationScheduleDto, ScheduleStatus } from '../types';

const SCHEDULE_STATUSES: ScheduleStatus[] = ['Pending', 'Dispatched', 'Cancelled'];

export default function ScheduleListPage() {
  const { t, i18n } = useTranslation('notifications');
  const { page, pageSize, setPage, setPageSize } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const canManage = hasPermission('notifications.schedule.manage');

  const { handleApiError } = useApiError();
  const [cancelId, setCancelId] = useState<string | null>(null);
  const [searchParams, setSearchParams] = useSearchParams();
  const rawStatus = searchParams.get('status');
  const status = SCHEDULE_STATUSES.includes(rawStatus as ScheduleStatus)
    ? (rawStatus as ScheduleStatus)
    : undefined;

  const { data, isPending } = useScheduledNotifications({ page, pageSize, status });

  const emptyStateNode = useMemo(() => (
    <EmptyState
      icon={CalendarClock}
      title={t('lockey_notifications_schedule_empty')}
      action={
        status
          ? {
              label: t('lockey_common_reset_filters', { ns: 'common' }),
              onClick: () => setSearchParams(new URLSearchParams()),
            }
          : undefined
      }
    />
  ), [status, t, setSearchParams]);
  const cancelSchedule = useCancelScheduledNotification();

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_notifications_module_name' },
      { label: 'lockey_notifications_schedule_title' },
    ]);
  }, [setBreadcrumbs]);

  const columns: ColumnDef<NotificationScheduleDto>[] = [
    {
      key: 'notificationId',
      header: t('lockey_notifications_schedule_col_notification'),
      render: (row) => row.notificationId.slice(0, 8) + '...',
    },
    {
      key: 'scheduledAt',
      header: t('lockey_notifications_schedule_col_scheduled_at'),
      render: (row) => new Date(row.scheduledAt).toLocaleString(i18n.language),
    },
    {
      key: 'status',
      header: t('lockey_notifications_schedule_col_status'),
      render: (row) => <ScheduleStatusBadge status={row.status} />,
    },
    {
      key: 'createdAt',
      header: t('lockey_notifications_schedule_col_created_at'),
      render: (row) => formatRelativeTime(row.createdAt),
    },
    {
      key: 'actions',
      header: t('lockey_notifications_schedule_col_actions'),
      render: (row) =>
        canManage && row.status === 'Pending' ? (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => setCancelId(row.id)}
          >
            {t('lockey_notifications_schedule_cancel')}
          </Button>
        ) : null,
    },
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">{t('lockey_notifications_schedule_title')}</h1>
        <p className="text-sm text-muted-foreground">
          {t('lockey_notifications_schedule_description')}
        </p>
      </div>

      <div className="flex flex-wrap items-center gap-4">
        <Select
          value={status ?? '__all__'}
          onValueChange={(v) => {
            setSearchParams((prev: URLSearchParams) => {
              const next = new URLSearchParams(prev);
              if (v === '__all__') { next.delete('status'); } else { next.set('status', v); }
              next.set('page', '1');
              return next;
            });
          }}
        >
          <SelectTrigger className="w-48" aria-label={t('lockey_notifications_schedule_filter_status')}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t('lockey_notifications_schedule_filter_all_statuses')}</SelectItem>
            {SCHEDULE_STATUSES.map((s) => (
              <SelectItem key={s} value={s}>
                {t('lockey_notifications_schedule_status_' + s.toLowerCase())}
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
        keyExtractor={(row) => row.id}
      />

      <ConfirmDialog
        open={cancelId !== null}
        onOpenChange={() => setCancelId(null)}
        title={t('lockey_notifications_schedule_confirm_cancel_title')}
        description={t('lockey_notifications_schedule_confirm_cancel')}
        variant="destructive"
        onConfirm={() => {
          if (cancelId) {
            cancelSchedule.mutate(cancelId, {
              onSuccess: () => setCancelId(null),
              onError: (err) => { handleApiError(err); setCancelId(null); },
            });
          }
        }}
        isPending={cancelSchedule.isPending}
      />
    </div>
  );
}
