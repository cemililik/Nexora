'use client';

import { useTranslations } from 'next-intl';

import { SectionRenderer } from '@/shared/components/layout/SectionRenderer';
import { useAuthStore } from '@/shared/lib/stores/authStore';
import { useCurrency } from '@/shared/hooks/useCurrency';
import { useOrganization } from '@/shared/hooks/useOrganization';
/**
 * Client component for the profile page — reads user data from Zustand store,
 * which is populated server-side by PortalShell before first render.
 * Client hooks (useOrganization, useCurrency) remain here since they
 * require client-side API calls.
 */
export function ProfileContent() {
  const tc = useTranslations('common');
  const tn = useTranslations('navigation');
  const user = useAuthStore((s) => s.user);
  const { organization } = useOrganization();
  const { defaultCurrency } = useCurrency();

  if (!user) return null;

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-foreground">
        {tn('lockey_nav_profile')}
      </h1>

      <div className="rounded-lg border border-border p-6 space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <p className="text-sm text-muted-foreground">{tc('lockey_common_profile_name')}</p>
            <p className="font-medium text-foreground">
              {user.firstName} {user.lastName}
            </p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">{tc('lockey_common_profile_email')}</p>
            <p className="font-medium text-foreground">{user.email}</p>
          </div>
          {organization && (
            <>
              <div>
                <p className="text-sm text-muted-foreground">{tc('lockey_common_profile_organization')}</p>
                <p className="font-medium text-foreground">{organization.name}</p>
              </div>
              <div>
                <p className="text-sm text-muted-foreground">{tc('lockey_common_profile_currency')}</p>
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
