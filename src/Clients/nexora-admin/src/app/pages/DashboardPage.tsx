import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Users, Contact, FileText, Bell, BarChart3, Activity } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';

import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { api } from '@/shared/lib/api';
import { useAuthStore } from '@/shared/lib/stores/authStore';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import type { PagedResult } from '@/shared/types/api';

interface StatsCard {
  label: string;
  icon: typeof Users;
  queryKey: string[];
  queryFn: () => Promise<{ totalCount: number }>;
}

/** Admin dashboard page with welcome message and live stats. */
export default function DashboardPage() {
  const { t } = useTranslation(['common', 'navigation', 'identity', 'contacts', 'documents', 'notifications']);
  const user = useAuthStore((s) => s.user);
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);

  useEffect(() => {
    setBreadcrumbs([{ label: 'lockey_nav_dashboard' }]);
  }, [setBreadcrumbs]);

  const cards: StatsCard[] = [
    {
      label: t('identity:lockey_identity_nav_users'),
      icon: Users,
      queryKey: ['dashboard', 'users'],
      queryFn: () => api.get<PagedResult<unknown>>('/identity/users', { page: 1, pageSize: 1 }).then((r) => ({ totalCount: r.totalCount })),
    },
    {
      label: t('contacts:lockey_contacts_nav_contacts'),
      icon: Contact,
      queryKey: ['dashboard', 'contacts'],
      queryFn: () => api.get<PagedResult<unknown>>('/contacts/contacts', { page: 1, pageSize: 1 }).then((r) => ({ totalCount: r.totalCount })),
    },
    {
      label: t('documents:lockey_documents_nav_documents'),
      icon: FileText,
      queryKey: ['dashboard', 'documents'],
      queryFn: () => api.get<PagedResult<unknown>>('/documents/documents', { page: 1, pageSize: 1 }).then((r) => ({ totalCount: r.totalCount })),
    },
    {
      label: t('notifications:lockey_notifications_nav_notifications'),
      icon: Bell,
      queryKey: ['dashboard', 'notifications'],
      queryFn: () => api.get<PagedResult<unknown>>('/notifications/notifications', { page: 1, pageSize: 1 }).then((r) => ({ totalCount: r.totalCount })),
    },
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">
          {t('common:lockey_common_welcome', { name: user?.firstName ?? '' })}
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          {t('common:lockey_common_dashboard_subtitle')}
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {cards.map((card) => (
          <StatsCardItem key={card.queryKey.join('.')} card={card} />
        ))}
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <Card>
          <CardHeader className="flex flex-row items-center gap-2 pb-2">
            <Activity className="h-4 w-4 text-muted-foreground" />
            <CardTitle className="text-sm font-medium text-muted-foreground">
              {t('common:lockey_common_recent_activity')}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              {t('common:lockey_common_no_recent_activity')}
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center gap-2 pb-2">
            <BarChart3 className="h-4 w-4 text-muted-foreground" />
            <CardTitle className="text-sm font-medium text-muted-foreground">
              {t('common:lockey_common_quick_stats')}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              {t('common:lockey_common_stats_coming_soon')}
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function StatsCardItem({ card }: { card: StatsCard }) {
  const { t } = useTranslation('common');
  const { data, isLoading } = useQuery({
    queryKey: card.queryKey,
    queryFn: card.queryFn,
    staleTime: 60_000,
    retry: false,
  });

  const Icon = card.icon;

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">
          {card.label}
        </CardTitle>
        <Icon className="h-4 w-4 text-muted-foreground" />
      </CardHeader>
      <CardContent>
        <p className="text-2xl font-bold">
          {isLoading ? t('lockey_common_loading') : (data?.totalCount ?? '–')}
        </p>
      </CardContent>
    </Card>
  );
}
