import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { Textarea } from '@/shared/components/ui/textarea';
import { Separator } from '@/shared/components/ui/separator';
import { FormField } from '@/shared/components/data/FormField';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { EmptyState } from '@/shared/components/feedback/EmptyState';
import { AlertCircle, Shield } from 'lucide-react';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import {
  useClearComplianceOverride,
  useComplianceConfig,
  useSetComplianceOverride,
} from '../hooks/useComplianceConfig';
import type { ComplianceKeySummary } from '../types/compliance';

/**
 * Organization-level compliance toggles (ADR-0025). Displays each managed key with
 * (tenant default, org override, effective value) and lets the admin set/clear the
 * org override. Platform caps are rendered as badges — forced or blocked caps disable
 * the toggle UI.
 *
 * Guarded at the route level via permission `contacts.gdpr.settings_manage`.
 */
export default function ComplianceSettingsPage() {
  const { t } = useTranslation('identity');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { data, isLoading, isError } = useComplianceConfig();

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_nav_compliance' },
      { label: 'lockey_identity_compliance_title' },
    ]);
  }, [setBreadcrumbs]);

  if (isLoading) return <LoadingSkeleton />;
  if (isError || !data) {
    return (
      <EmptyState
        icon={AlertCircle}
        title={t('lockey_identity_compliance_load_failed')}
        description={t('lockey_identity_compliance_load_failed_description')}
      />
    );
  }

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">
          {t('lockey_identity_compliance_title')}
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          {t('lockey_identity_compliance_description')}
        </p>
      </div>

      {data.length === 0 ? (
        <EmptyState
          icon={Shield}
          title={t('lockey_identity_compliance_empty_title')}
          description={t('lockey_identity_compliance_empty_description')}
        />
      ) : (
        data.map((item) => <ComplianceRow key={item.key} item={item} />)
      )}
    </div>
  );
}

interface ComplianceRowProps {
  readonly item: ComplianceKeySummary;
}

function ComplianceRow({ item }: ComplianceRowProps) {
  const reasonFieldId = `compliance-reason-${keyToSlug(item.key)}`;
  const { t } = useTranslation('identity');
  const setOverride = useSetComplianceOverride(item.key);
  const clearOverride = useClearComplianceOverride(item.key);

  const [reason, setReason] = useState('');
  const hasOrgOverride = item.orgOverride !== null;
  const capBlocksChanges = item.capForced || !item.capAllowed;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex flex-wrap items-center gap-2">
          <span>{t(`lockey_identity_compliance_key_${keyToSlug(item.key)}`)}</span>
          <CapBadge item={item} />
          <WinnerBadge layer={item.winningLayer} />
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="text-sm text-muted-foreground">
          {t(`lockey_identity_compliance_key_${keyToSlug(item.key)}_description`)}
        </p>

        <dl className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-3">
          <div>
            <dt className="text-muted-foreground">
              {t('lockey_identity_compliance_effective_value')}
            </dt>
            <dd className="font-medium">
              {item.effectiveValue
                ? t('lockey_identity_compliance_enabled')
                : t('lockey_identity_compliance_disabled')}
            </dd>
          </div>
          <div>
            <dt className="text-muted-foreground">
              {t('lockey_identity_compliance_tenant_default')}
            </dt>
            <dd>{formatLayerValue(item.tenantDefault, t)}</dd>
          </div>
          <div>
            <dt className="text-muted-foreground">
              {t('lockey_identity_compliance_org_override')}
            </dt>
            <dd>{formatLayerValue(item.orgOverride, t)}</dd>
          </div>
        </dl>

        {!capBlocksChanges && (
          <>
            <Separator />
            <div className="space-y-2">
              <FormField
                label={t('lockey_identity_compliance_reason')}
                htmlFor={reasonFieldId}
                required
              >
                <Textarea
                  id={reasonFieldId}
                  value={reason}
                  maxLength={500}
                  onChange={(e) => { setReason(e.target.value); }}
                  placeholder={t('lockey_identity_compliance_reason_placeholder')}
                />
              </FormField>
              <div className="flex flex-wrap items-center gap-2">
                <Button
                  type="button"
                  disabled={!reason.trim() || setOverride.isPending}
                  onClick={() => {
                    setOverride.mutate(
                      { value: !item.effectiveValue, reason: reason.trim() },
                      { onSuccess: () => { setReason(''); } },
                    );
                  }}
                >
                  {item.effectiveValue
                    ? t('lockey_identity_compliance_action_disable')
                    : t('lockey_identity_compliance_action_enable')}
                </Button>
                {hasOrgOverride && (
                  <Button
                    type="button"
                    variant="outline"
                    disabled={!reason.trim() || clearOverride.isPending}
                    onClick={() => {
                      clearOverride.mutate(reason.trim(), {
                        onSuccess: () => { setReason(''); },
                      });
                    }}
                  >
                    {t('lockey_identity_compliance_action_clear_override')}
                  </Button>
                )}
              </div>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}

function CapBadge({ item }: { readonly item: ComplianceKeySummary }) {
  const { t } = useTranslation('identity');
  if (item.capForced) {
    return (
      <Badge variant="secondary" title={t('lockey_identity_compliance_cap_forced_hint')}>
        {t('lockey_identity_compliance_cap_forced')}
      </Badge>
    );
  }
  if (!item.capAllowed) {
    return (
      <Badge variant="destructive" title={t('lockey_identity_compliance_cap_blocked_hint')}>
        {t('lockey_identity_compliance_cap_blocked')}
      </Badge>
    );
  }
  return null;
}

function WinnerBadge({ layer }: { readonly layer: ComplianceKeySummary['winningLayer'] }) {
  const { t } = useTranslation('identity');
  const key = `lockey_identity_compliance_winner_${layer.toLowerCase()}`;
  return (
    <Badge variant="outline" title={t(key)}>
      {t(key)}
    </Badge>
  );
}

function formatLayerValue(
  value: boolean | null,
  t: (k: string) => string,
): string {
  if (value === null) return t('lockey_identity_compliance_unset');
  return value
    ? t('lockey_identity_compliance_enabled')
    : t('lockey_identity_compliance_disabled');
}

function keyToSlug(key: string): string {
  return key.replace(/\./g, '_');
}
