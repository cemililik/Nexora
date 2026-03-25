'use client';

import { useTranslations } from 'next-intl';

import { SectionRenderer } from '@/shared/components/layout/SectionRenderer';
import { useAuthStore } from '@/shared/lib/stores/authStore';
/**
 * Client component for the dashboard — reads user data from Zustand store,
 * which is populated server-side by PortalShell before first render.
 */
export function DashboardContent() {
  const t = useTranslations('common');
  const user = useAuthStore((s) => s.user);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-foreground">
          {t('lockey_common_dashboard')}
        </h1>
        {user && (
          <p className="mt-1 text-muted-foreground">
            {t('lockey_common_welcome', { name: user.firstName })}
          </p>
        )}
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <SectionRenderer position="dashboard-main" className="space-y-6" />
        </div>
        <div>
          <SectionRenderer position="dashboard-sidebar" className="space-y-6" />
        </div>
      </div>
    </div>
  );
}
