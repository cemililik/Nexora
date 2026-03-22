import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { cn } from '@/shared/lib/utils';
import type { ConsentRecordDto, ConsentType, RecordConsentRequest } from '../types';

const toSnakeCase = (str: string) => str.replace(/([A-Z])/g, '_$1').toLowerCase().replace(/^_/, '');

const consentTypes: ConsentType[] = ['EmailMarketing', 'SmsMarketing', 'DataProcessing'];

interface ConsentPanelProps {
  consents: ConsentRecordDto[];
  onRecord: (data: RecordConsentRequest) => void;
  isPending: boolean;
}

export function ConsentPanel({ consents, onRecord, isPending }: ConsentPanelProps) {
  const { t, i18n } = useTranslation('contacts');

  function getLatestConsent(consentType: ConsentType): ConsentRecordDto | undefined {
    return consents
      .filter((c) => c.consentType === consentType)
      .sort((a, b) => new Date(b.grantedAt).getTime() - new Date(a.grantedAt).getTime())[0];
  }

  function handleToggle(consentType: ConsentType, currentlyGranted: boolean) {
    onRecord({
      consentType,
      granted: !currentlyGranted,
      source: 'AdminPanel',
    });
  }

  return (
    <div className="space-y-3">
      {consentTypes.map((consentType) => {
        const latest = getLatestConsent(consentType);
        const isGranted = latest?.granted ?? false;

        return (
          <div
            key={consentType}
            className="flex items-center justify-between rounded-md border p-3"
          >
            <div className="space-y-1">
              <p className="text-sm font-medium">
                {t(`lockey_contacts_consent_type_${toSnakeCase(consentType)}`)}
              </p>
              <div className="flex items-center gap-2">
                <Badge
                  variant="outline"
                  className={cn(
                    isGranted
                      ? 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200'
                      : 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200'
                  )}
                >
                  {isGranted
                    ? t('lockey_contacts_consent_granted')
                    : t('lockey_contacts_consent_revoked')}
                </Badge>
                {latest && (
                  <span className="text-xs text-muted-foreground">
                    {new Date(latest.grantedAt).toLocaleDateString(i18n.language)}
                  </span>
                )}
              </div>
            </div>
            <Button
              type="button"
              variant={isGranted ? 'outline' : 'default'}
              size="sm"
              onClick={() => handleToggle(consentType, isGranted)}
              disabled={isPending}
            >
              {isGranted
                ? t('lockey_contacts_consent_revoke')
                : t('lockey_contacts_consent_grant')}
            </Button>
          </div>
        );
      })}
    </div>
  );
}
