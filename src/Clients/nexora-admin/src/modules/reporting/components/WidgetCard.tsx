import { useTranslation } from 'react-i18next';

import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { useWidgetData } from '../hooks/useDashboards';
import type { DashboardWidget } from '../types';
import { ChartWidget } from './ChartWidget';
import { KpiWidget } from './KpiWidget';
import { TableWidget } from './TableWidget';

interface WidgetCardProps {
  dashboardId: string;
  widget: DashboardWidget;
}

export function WidgetCard({ dashboardId, widget }: WidgetCardProps) {
  const { t } = useTranslation('reporting');
  const { data, isLoading, isError } = useWidgetData(dashboardId, widget.id);

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium">{widget.title}</CardTitle>
      </CardHeader>
      <CardContent>
        {isLoading && (
          <div className="flex h-32 items-center justify-center text-muted-foreground">
            {t('lockey_reporting_widget_loading')}
          </div>
        )}
        {isError && (
          <div className="flex h-32 items-center justify-center text-destructive">
            {t('lockey_reporting_widget_error')}
          </div>
        )}
        {data && widget.type === 'Chart' && (
          <ChartWidget data={data} chartType={widget.chartType ?? 'Bar'} />
        )}
        {data && widget.type === 'Kpi' && <KpiWidget data={data} />}
        {data && widget.type === 'Table' && <TableWidget data={data} />}
      </CardContent>
    </Card>
  );
}
