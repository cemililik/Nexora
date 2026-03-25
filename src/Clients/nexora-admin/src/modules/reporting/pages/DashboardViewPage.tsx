import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';

import { useDashboard } from '../hooks/useDashboards';
import { DashboardGrid } from '../components/DashboardGrid';
import type { DashboardWidget } from '../types';

export default function DashboardViewPage() {
  const { t } = useTranslation('reporting');
  const { id } = useParams<{ id: string }>();
  const { data: dashboard, isLoading } = useDashboard(id!);

  if (isLoading || !dashboard) {
    return <p className="text-muted-foreground">{t('lockey_reporting_loading')}</p>;
  }

  const widgets: DashboardWidget[] = dashboard.widgets
    ? (JSON.parse(dashboard.widgets) as DashboardWidget[])
    : [];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-foreground">{dashboard.name}</h1>
        {dashboard.description && (
          <p className="mt-1 text-muted-foreground">{dashboard.description}</p>
        )}
      </div>

      {widgets.length === 0 ? (
        <p className="text-muted-foreground">{t('lockey_reporting_no_widgets')}</p>
      ) : (
        <DashboardGrid dashboardId={dashboard.id} widgets={widgets} />
      )}
    </div>
  );
}
