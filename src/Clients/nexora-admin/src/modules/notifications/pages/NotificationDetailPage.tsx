import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';

import { useUiStore } from '@/shared/lib/stores/uiStore';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { useNotification } from '../hooks/useNotifications';
import { NotificationStatusBadge } from '../components/NotificationStatusBadge';
import { ChannelBadge } from '../components/ChannelBadge';
import { RecipientStatusBadge } from '../components/RecipientStatusBadge';

export default function NotificationDetailPage() {
  const { t, i18n } = useTranslation('notifications');
  const { id } = useParams<{ id: string }>();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);

  const { data: notification, isPending } = useNotification(id ?? '');

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_notifications_module_name', path: '/notifications/notifications' },
      { label: 'lockey_notifications_list_title', path: '/notifications/notifications' },
      { label: 'lockey_notifications_detail_title' },
    ]);
  }, [setBreadcrumbs]);

  if (isPending) return <LoadingSkeleton />;
  if (!notification) return null;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">{t('lockey_notifications_detail_title')}</h1>
      </div>

      {/* Summary */}
      <div className="rounded-lg border p-4 space-y-3">
        <div className="grid grid-cols-2 gap-4 text-sm md:grid-cols-4">
          <div>
            <span className="text-muted-foreground">{t('lockey_notifications_col_subject')}</span>
            <p className="font-medium">{notification.subject}</p>
          </div>
          <div>
            <span className="text-muted-foreground">{t('lockey_notifications_col_channel')}</span>
            <p><ChannelBadge channel={notification.channel} /></p>
          </div>
          <div>
            <span className="text-muted-foreground">{t('lockey_notifications_col_status')}</span>
            <p><NotificationStatusBadge status={notification.status} /></p>
          </div>
          <div>
            <span className="text-muted-foreground">{t('lockey_notifications_col_triggered_by')}</span>
            <p className="font-medium">{notification.triggeredBy}</p>
          </div>
        </div>
        <div className="grid grid-cols-2 gap-4 text-sm md:grid-cols-5">
          <div>
            <span className="text-muted-foreground">{t('lockey_notifications_col_recipients')}</span>
            <p className="font-medium">{notification.totalRecipients}</p>
          </div>
          <div>
            <span className="text-muted-foreground">{t('lockey_notifications_col_delivered')}</span>
            <p className="font-medium">{notification.deliveredCount}</p>
          </div>
          <div>
            <span className="text-muted-foreground">{t('lockey_notifications_col_failed')}</span>
            <p className="font-medium">{notification.failedCount}</p>
          </div>
          <div>
            <span className="text-muted-foreground">{t('lockey_notifications_detail_opened')}</span>
            <p className="font-medium">{notification.openedCount}</p>
          </div>
          <div>
            <span className="text-muted-foreground">{t('lockey_notifications_detail_clicked')}</span>
            <p className="font-medium">{notification.clickedCount}</p>
          </div>
        </div>
        <div className="grid grid-cols-2 gap-4 text-sm">
          <div>
            <span className="text-muted-foreground">{t('lockey_notifications_col_queued_at')}</span>
            <p className="font-medium">
              {new Date(notification.queuedAt).toLocaleString(i18n.language)}
            </p>
          </div>
          {notification.sentAt && (
            <div>
              <span className="text-muted-foreground">{t('lockey_notifications_col_sent_at')}</span>
              <p className="font-medium">
                {new Date(notification.sentAt).toLocaleString(i18n.language)}
              </p>
            </div>
          )}
        </div>
      </div>

      {/* Body */}
      <div className="rounded-lg border p-4">
        <h2 className="mb-2 text-lg font-medium">{t('lockey_notifications_detail_body')}</h2>
        <div className="whitespace-pre-wrap rounded bg-muted p-3 text-sm">
          {notification.bodyRendered}
        </div>
      </div>

      {/* Recipients */}
      <div className="rounded-lg border">
        <div className="border-b px-4 py-3">
          <h2 className="text-lg font-medium">{t('lockey_notifications_detail_recipients_title')}</h2>
        </div>
        <table className="w-full text-sm" aria-label={t('lockey_notifications_aria_recipients_table')}>
          <thead>
            <tr className="border-b bg-muted/50">
              <th className="px-4 py-2 text-start">{t('lockey_notifications_detail_col_address')}</th>
              <th className="px-4 py-2 text-start">{t('lockey_notifications_detail_col_status')}</th>
              <th className="px-4 py-2 text-start">{t('lockey_notifications_detail_col_failure_reason')}</th>
              <th className="px-4 py-2 text-start">{t('lockey_notifications_detail_col_sent_at')}</th>
              <th className="px-4 py-2 text-start">{t('lockey_notifications_detail_col_delivered_at')}</th>
              <th className="px-4 py-2 text-start">{t('lockey_notifications_detail_col_opened_at')}</th>
            </tr>
          </thead>
          <tbody>
            {notification.recipients.map((r) => (
              <tr key={r.id} className="border-b last:border-0">
                <td className="px-4 py-2">{r.recipientAddress}</td>
                <td className="px-4 py-2"><RecipientStatusBadge status={r.status} /></td>
                <td className="px-4 py-2 text-muted-foreground">{r.failureReason ?? '-'}</td>
                <td className="px-4 py-2">
                  {r.sentAt ? new Date(r.sentAt).toLocaleString(i18n.language) : '-'}
                </td>
                <td className="px-4 py-2">
                  {r.deliveredAt ? new Date(r.deliveredAt).toLocaleString(i18n.language) : '-'}
                </td>
                <td className="px-4 py-2">
                  {r.openedAt ? new Date(r.openedAt).toLocaleString(i18n.language) : '-'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
