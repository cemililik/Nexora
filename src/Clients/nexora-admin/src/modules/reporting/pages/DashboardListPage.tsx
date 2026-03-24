import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { useDashboards } from '../hooks/useDashboards';

export default function DashboardListPage() {
  const { t } = useTranslation('reporting');
  const { data, isLoading } = useDashboards({ page: 1, pageSize: 50 });

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-foreground">
          {t('lockey_reporting_nav_dashboards')}
        </h1>
        <Button asChild>
          <Link to="/reporting/dashboards/create">
            {t('lockey_reporting_action_create_dashboard')}
          </Link>
        </Button>
      </div>

      {isLoading && (
        <p className="text-muted-foreground">{t('lockey_reporting_loading')}</p>
      )}

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {data?.items.map((dashboard) => (
          <Link key={dashboard.id} to={`/reporting/dashboards/${dashboard.id}`}>
            <Card className="transition-colors hover:border-primary">
              <CardHeader className="pb-2">
                <div className="flex items-center justify-between">
                  <CardTitle className="text-base">{dashboard.name}</CardTitle>
                  {dashboard.isDefault && (
                    <span className="rounded-full bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary">
                      {t('lockey_reporting_default')}
                    </span>
                  )}
                </div>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground line-clamp-2">
                  {dashboard.description ?? t('lockey_reporting_no_description')}
                </p>
              </CardContent>
            </Card>
          </Link>
        ))}
        {data?.items.length === 0 && (
          <p className="text-muted-foreground">{t('lockey_reporting_no_dashboards')}</p>
        )}
      </div>
    </div>
  );
}
