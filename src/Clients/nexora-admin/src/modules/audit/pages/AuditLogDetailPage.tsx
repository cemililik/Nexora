import { useEffect, useState } from 'react';
import { useParams } from 'react-router';
import { useTranslation } from 'react-i18next';
import { ChevronDown } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { formatRelativeTime } from '@/shared/lib/date';
import { cn } from '@/shared/lib/utils';
import { useAuditLogDetail } from '../hooks/useAuditLogDetail';
import { AuditStatusBadge } from '../components/AuditStatusBadge';
import { AuditOperationTypeBadge } from '../components/AuditOperationTypeBadge';
import { EntityDiffViewer } from '../components/EntityDiffViewer';

function JsonSection({ title, json }: { title: string; json: string | null | undefined }) {
  const [open, setOpen] = useState(false);

  if (!json) return null;

  let formatted: string;
  try {
    formatted = JSON.stringify(JSON.parse(json), null, 2);
  } catch {
    formatted = json;
  }

  return (
    <div className="rounded-md border">
      <button
        type="button"
        onClick={() => setOpen(!open)}
        className="flex w-full items-center justify-between px-4 py-3 text-sm font-medium hover:bg-accent transition-colors"
      >
        <span>{title}</span>
        <ChevronDown className={cn('h-4 w-4 transition-transform', open && 'rotate-180')} />
      </button>
      {open && (
        <pre className="overflow-x-auto border-t bg-muted/30 px-4 py-3 text-xs">
          {formatted}
        </pre>
      )}
    </div>
  );
}

export default function AuditLogDetailPage() {
  const { id = '' } = useParams<{ id: string }>();
  const { t, i18n } = useTranslation('audit');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);

  const { data: log, isPending } = useAuditLogDetail(id);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_audit_module_name' },
      { label: 'lockey_audit_nav_logs', path: '/audit/logs' },
      { label: log ? `${log.module} / ${log.operation}` : '...' },
    ]);
  }, [setBreadcrumbs, log]);

  if (isPending) return <LoadingSkeleton lines={8} />;
  if (!log) return null;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">{t('lockey_audit_detail_title')}</h1>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Summary */}
        <Card>
          <CardHeader>
            <CardTitle>{t('lockey_audit_detail_summary')}</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="space-y-3">
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_audit_col_module')}</dt>
                <dd>{log.module}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_audit_col_operation')}</dt>
                <dd>{log.operation}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_audit_col_type')}</dt>
                <dd><AuditOperationTypeBadge operationType={log.operationType} /></dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_audit_col_status')}</dt>
                <dd><AuditStatusBadge isSuccess={log.isSuccess} /></dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_audit_col_timestamp')}</dt>
                <dd>{new Date(log.timestamp).toLocaleString(i18n.language)} ({formatRelativeTime(log.timestamp)})</dd>
              </div>
            </dl>
          </CardContent>
        </Card>

        {/* User Information */}
        <Card>
          <CardHeader>
            <CardTitle>{t('lockey_audit_detail_user_info')}</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="space-y-3">
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_audit_col_user')}</dt>
                <dd>{log.userEmail}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">User ID</dt>
                <dd>{log.userId ?? '—'}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_audit_detail_ip_address')}</dt>
                <dd>{log.ipAddress ?? '—'}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_audit_detail_user_agent')}</dt>
                <dd className="break-all text-xs">{log.userAgent ?? '—'}</dd>
              </div>
            </dl>
          </CardContent>
        </Card>
      </div>

      {/* Entity Information */}
      <Card>
        <CardHeader>
          <CardTitle>{t('lockey_audit_detail_entity_info')}</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid gap-3 sm:grid-cols-3">
            <div>
              <dt className="text-sm text-muted-foreground">{t('lockey_audit_col_entity')}</dt>
              <dd>{log.entityType ?? '—'}</dd>
            </div>
            <div>
              <dt className="text-sm text-muted-foreground">Entity ID</dt>
              <dd className="break-all text-xs">{log.entityId ?? '—'}</dd>
            </div>
            <div>
              <dt className="text-sm text-muted-foreground">{t('lockey_audit_detail_correlation_id')}</dt>
              <dd className="break-all text-xs">{log.correlationId ?? '—'}</dd>
            </div>
          </dl>
        </CardContent>
      </Card>

      {/* Error Key */}
      {log.errorKey && (
        <Card>
          <CardHeader>
            <CardTitle>{t('lockey_audit_detail_error_key')}</CardTitle>
          </CardHeader>
          <CardContent>
            <code className="rounded bg-destructive/10 px-2 py-1 text-sm text-destructive">
              {log.errorKey}
            </code>
          </CardContent>
        </Card>
      )}

      {/* Changes */}
      {log.changes && (
        <Card>
          <CardHeader>
            <CardTitle>{t('lockey_audit_detail_changes')}</CardTitle>
          </CardHeader>
          <CardContent>
            <EntityDiffViewer changes={log.changes} />
          </CardContent>
        </Card>
      )}

      {/* Before / After State */}
      <div className="space-y-4">
        <JsonSection title={t('lockey_audit_detail_before_state')} json={log.beforeState} />
        <JsonSection title={t('lockey_audit_detail_after_state')} json={log.afterState} />
      </div>
    </div>
  );
}
