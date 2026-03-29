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
import { useNotifications } from '../hooks/useNotifications';
import { NotificationStatusBadge } from '../components/NotificationStatusBadge';
import { ChannelBadge } from '../components/ChannelBadge';
import { CHANNELS, CHANNEL_KEY_MAP, NOTIFICATION_STATUSES, STATUS_KEY_MAP } from '../constants';
import type { NotificationDto, NotificationChannel, NotificationStatus } from '../types';

export default function NotificationListPage() {
  const { t } = useTranslation('notifications');
  const navigate = useNavigate();
  const { page, pageSize, setPage } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const canSend = hasPermission('notifications.notification.send');

  const [searchParams, setSearchParams] = useSearchParams();
  const rawChannel = searchParams.get('channel');
  const channel = CHANNELS.includes(rawChannel as NotificationChannel) ? (rawChannel as NotificationChannel) : undefined;
  const rawStatus = searchParams.get('status');
  const status = NOTIFICATION_STATUSES.includes(rawStatus as NotificationStatus) ? (rawStatus as NotificationStatus) : undefined;

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_notifications_module_name' },
      { label: 'lockey_notifications_list_title' },
    ]);
  }, [setBreadcrumbs]);

  const { data, isPending } = useNotifications({ page, pageSize, channel, status });

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

  const columns: ColumnDef<NotificationDto>[] = [
    {
      key: 'subject',
      header: t('lockey_notifications_col_subject'),
      render: (row) => row.subject,
    },
    {
      key: 'channel',
      header: t('lockey_notifications_col_channel'),
      render: (row) => <ChannelBadge channel={row.channel} />,
    },
    {
      key: 'status',
      header: t('lockey_notifications_col_status'),
      render: (row) => <NotificationStatusBadge status={row.status} />,
    },
    {
      key: 'recipients',
      header: t('lockey_notifications_col_recipients'),
      render: (row) => row.totalRecipients,
    },
    {
      key: 'delivered',
      header: t('lockey_notifications_col_delivered'),
      render: (row) => row.deliveredCount,
    },
    {
      key: 'failed',
      header: t('lockey_notifications_col_failed'),
      render: (row) => row.failedCount,
    },
    {
      key: 'queuedAt',
      header: t('lockey_notifications_col_queued_at'),
      render: (row) => formatRelativeTime(row.queuedAt),
    },
    {
      key: 'actions',
      header: t('lockey_notifications_col_actions'),
      render: (row) => (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => navigate(`/notifications/notifications/${row.id}`)}
        >
          {t('lockey_notifications_action_view')}
        </Button>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('lockey_notifications_list_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_notifications_list_description')}
          </p>
        </div>
        {canSend && (
          <Button type="button" onClick={() => navigate('/notifications/send')}>
            {t('lockey_notifications_send_action')}
          </Button>
        )}
      </div>

      <div className="flex items-center gap-4">
        <Select
          value={channel ?? '__all__'}
          onValueChange={(v) => updateFilter('channel', v === '__all__' ? '' : v)}
        >
          <SelectTrigger className="w-48" aria-label={t('lockey_notifications_aria_channel_filter')}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t('lockey_notifications_filter_all_channels')}</SelectItem>
            {CHANNELS.map((c) => (
              <SelectItem key={c} value={c}>
                {t(CHANNEL_KEY_MAP[c])}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select
          value={status ?? '__all__'}
          onValueChange={(v) => updateFilter('status', v === '__all__' ? '' : v)}
        >
          <SelectTrigger className="w-48" aria-label={t('lockey_notifications_aria_status_filter')}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t('lockey_notifications_filter_all_statuses')}</SelectItem>
            {NOTIFICATION_STATUSES.map((s) => (
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
        isLoading={isPending}
        emptyMessage={t('lockey_notifications_empty_notifications')}
        keyExtractor={(row) => row.id}
      />
    </div>
  );
}
