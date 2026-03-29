import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';

import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
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
import { useNotificationTemplates } from '../hooks/useNotificationTemplates';
import { ChannelBadge } from '../components/ChannelBadge';
import { CHANNELS, CHANNEL_KEY_MAP } from '../constants';
import type { NotificationTemplateDto, NotificationChannel } from '../types';

export default function TemplateListPage() {
  const { t } = useTranslation('notifications');
  const navigate = useNavigate();
  const { page, pageSize, setPage } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const canManage = hasPermission('notifications.template.manage');

  const [searchParams, setSearchParams] = useSearchParams();
  const rawChannel = searchParams.get('channel');
  const channel = CHANNELS.includes(rawChannel as NotificationChannel) ? (rawChannel as NotificationChannel) : undefined;

  // Remove invalid channel param from URL
  useEffect(() => {
    if (rawChannel && !CHANNELS.includes(rawChannel as NotificationChannel)) {
      setSearchParams((prev: URLSearchParams) => {
        const next = new URLSearchParams(prev);
        next.delete('channel');
        return next;
      }, { replace: true });
    }
  }, [rawChannel, setSearchParams]);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_notifications_module_name' },
      { label: 'lockey_notifications_templates_title' },
    ]);
  }, [setBreadcrumbs]);

  const { data, isPending } = useNotificationTemplates({ page, pageSize, channel });

  const columns: ColumnDef<NotificationTemplateDto>[] = [
    {
      key: 'code',
      header: t('lockey_notifications_templates_col_code'),
      render: (row) => row.code,
    },
    {
      key: 'module',
      header: t('lockey_notifications_templates_col_module'),
      render: (row) => row.module,
    },
    {
      key: 'channel',
      header: t('lockey_notifications_templates_col_channel'),
      render: (row) => <ChannelBadge channel={row.channel} />,
    },
    {
      key: 'subject',
      header: t('lockey_notifications_templates_col_subject'),
      render: (row) => row.subject,
    },
    {
      key: 'format',
      header: t('lockey_notifications_templates_col_format'),
      render: (row) =>
        t(`lockey_notifications_templates_format_${row.format.toLowerCase()}`),
    },
    {
      key: 'isActive',
      header: t('lockey_notifications_templates_col_active'),
      render: (row) => (
        <Badge variant={row.isActive ? 'default' : 'secondary'}>
          {row.isActive ? t('lockey_notifications_templates_col_active') : '-'}
        </Badge>
      ),
    },
    {
      key: 'createdAt',
      header: t('lockey_notifications_templates_col_created_at'),
      render: (row) => formatRelativeTime(row.createdAt),
    },
    ...(canManage ? [{
      key: 'actions' as const,
      header: t('lockey_notifications_templates_col_actions'),
      render: (row: NotificationTemplateDto) => (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => navigate(`/notifications/templates/${row.id}`)}
        >
          {t('lockey_notifications_templates_edit')}
        </Button>
      ),
    }] : []),
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('lockey_notifications_templates_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_notifications_templates_description')}
          </p>
        </div>
        {canManage && (
          <Button type="button" onClick={() => navigate('/notifications/templates/create')}>
            {t('lockey_notifications_templates_create')}
          </Button>
        )}
      </div>

      <div className="flex items-center gap-4">
        <Select
          value={channel ?? '__all__'}
          onValueChange={(v) => {
            setSearchParams((prev: URLSearchParams) => {
              const next = new URLSearchParams(prev);
              if (v === '__all__') {
                next.delete('channel');
              } else {
                next.set('channel', v);
              }
              next.set('page', '1');
              return next;
            });
          }}
        >
          <SelectTrigger className="w-48" aria-label={t('lockey_notifications_aria_channel_filter')}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t('lockey_notifications_templates_filter_all_channels')}</SelectItem>
            {CHANNELS.map((c) => (
              <SelectItem key={c} value={c}>
                {t(CHANNEL_KEY_MAP[c])}
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
        emptyMessage={t('lockey_notifications_templates_empty')}
        keyExtractor={(row) => row.id}
      />
    </div>
  );
}
