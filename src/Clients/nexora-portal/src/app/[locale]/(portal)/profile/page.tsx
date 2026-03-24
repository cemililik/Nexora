'use client';

import { useTranslations } from 'next-intl';

import { SectionRenderer } from '@/shared/components/layout/SectionRenderer';
import { useAuthStore } from '@/shared/lib/stores/authStore';
import { useCurrency } from '@/shared/hooks/useCurrency';
import { useOrganization } from '@/shared/hooks/useOrganization';

/**
 * User profile page — shows current user info, organization details,
 * and module-contributed profile sections.
 */
export default function ProfilePage() {
  const t = useTranslations();
  const user = useAuthStore((s) => s.user);
  const { organization } = useOrganization();
  const { defaultCurrency } = useCurrency();

  if (!user) return null;

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-foreground">
        {t('lockey_nav_profile')}
      </h1>

      <div className="rounded-lg border border-border p-6 space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <p className="text-sm text-muted-foreground">{t('lockey_common_profile_name')}</p>
            <p className="font-medium text-foreground">
              {user.firstName} {user.lastName}
            </p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">{t('lockey_common_profile_email')}</p>
            <p className="font-medium text-foreground">{user.email}</p>
          </div>
          {organization && (
            <>
              <div>
                <p className="text-sm text-muted-foreground">{t('lockey_common_profile_organization')}</p>
                <p className="font-medium text-foreground">{organization.name}</p>
              </div>
              <div>
                <p className="text-sm text-muted-foreground">{t('lockey_common_profile_currency')}</p>
                <p className="font-medium text-foreground">{defaultCurrency}</p>
              </div>
            </>
          )}
        </div>
      </div>

      <SectionRenderer position="profile" className="space-y-6" />
    </div>
  );
}
