import { useEffect, useState } from 'react';
import { useParams } from 'react-router';
import { useTranslation } from 'react-i18next';

import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { formatRelativeTime } from '@/shared/lib/date';
import { cn } from '@/shared/lib/utils';
import { useAuditLogDetail } from '../hooks/useAuditLogDetail';
import { AuditStatusBadge } from '../components/AuditStatusBadge';
import { AuditOperationTypeBadge } from '../components/AuditOperationTypeBadge';
import { EntityDiffViewer } from '../components/EntityDiffViewer';

const TABS = [
  { id: 'overview', labelKey: 'lockey_audit_detail_tab_overview' },
  { id: 'changes', labelKey: 'lockey_audit_detail_tab_changes' },
] as const;

type TabId = (typeof TABS)[number]['id'];

export default function AuditLogDetailPage() {
  const { id = '' } = useParams<{ id: string }>();
  const { t, i18n } = useTranslation('audit');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const [activeTab, setActiveTab] = useState<TabId>('overview');

  const { data: log, isPending } = useAuditLogDetail(id);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_audit_module_name' },
      { label: 'lockey_audit_nav_logs', path: '/audit/logs' },
      { label: log ? `${t('lockey_audit_module_' + log.module, { defaultValue: log.module })} / ${t('lockey_audit_operation_' + log.operation.toLowerCase(), { defaultValue: log.operation })}` : '...' },
    ]);
  }, [setBreadcrumbs, log, t]);

  function handleTabChange(tab: TabId) {
    setActiveTab(tab);
    window.scrollTo(0, 0);
  }

  if (isPending) return <LoadingSkeleton lines={8} />;
  if (!log) return null;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">{t('lockey_audit_detail_title')}</h1>
      </div>

      {/* Tab navigation */}
      <div className="border-b">
        <div className="-mb-px flex gap-6">
          {TABS.map((tab) => (
            <button
              key={tab.id}
              type="button"
              onClick={() => handleTabChange(tab.id)}
              className={cn(
                'pb-3 text-sm font-medium transition-colors border-b-2',
                activeTab === tab.id
                  ? 'border-primary text-foreground'
                  : 'border-transparent text-muted-foreground hover:text-foreground',
              )}
            >
              {t(tab.labelKey)}
            </button>
          ))}
        </div>
      </div>

      {/* Overview Tab */}
      {activeTab === 'overview' && (
        <div className="space-y-6">
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
                    <dd>{t('lockey_audit_module_' + log.module, { defaultValue: log.module })}</dd>
                  </div>
                  <div>
                    <dt className="text-sm text-muted-foreground">{t('lockey_audit_col_operation')}</dt>
                    <dd>{t('lockey_audit_operation_' + log.operation.toLowerCase(), { defaultValue: log.operation })}</dd>
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
                    <dt className="text-sm text-muted-foreground">{t('lockey_audit_col_user_id')}</dt>
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
                  <dt className="text-sm text-muted-foreground">{t('lockey_audit_col_entity_id')}</dt>
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
        </div>
      )}

      {/* Changes Tab — shows only the human-readable field-level diff.
          Raw BeforeState/AfterState JSON is intentionally not rendered: it exposes
          internal schema (strongly-typed IDs, tenant scoping fields) and is retained
          server-side for compliance/forensics only. */}
      {activeTab === 'changes' && (
        <Card>
          <CardHeader>
            <CardTitle>{t('lockey_audit_detail_changes')}</CardTitle>
          </CardHeader>
          <CardContent>
            <EntityDiffViewer changes={log.changes} />
          </CardContent>
        </Card>
      )}
    </div>
  );
}
