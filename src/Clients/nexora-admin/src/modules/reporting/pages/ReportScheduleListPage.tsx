import { useTranslation } from 'react-i18next';

import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { useReportSchedules, useDeleteReportSchedule } from '../hooks/useReportSchedules';
import { Button } from '@/shared/components/ui/button';

export default function ReportScheduleListPage() {
  const { t } = useTranslation('reporting');
  const { data, isLoading } = useReportSchedules({ page: 1, pageSize: 50 });
  const deleteSchedule = useDeleteReportSchedule();

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-foreground">
        {t('lockey_reporting_nav_schedules')}
      </h1>

      {isLoading && (
        <p className="text-muted-foreground">{t('lockey_reporting_loading')}</p>
      )}

      <div className="space-y-3">
        {data?.items.map((schedule) => (
          <Card key={schedule.id}>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">
                {t('lockey_reporting_schedule_for', { id: schedule.definitionId.slice(0, 8) })}
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex items-center justify-between">
                <div className="space-y-1 text-sm">
                  <p>
                    <span className="text-muted-foreground">{t('lockey_reporting_col_cron')}:</span>{' '}
                    <code className="rounded bg-muted px-1">{schedule.cronExpression}</code>
                  </p>
                  <p>
                    <span className="text-muted-foreground">{t('lockey_reporting_col_format')}:</span>{' '}
                    {schedule.format}
                  </p>
                  {schedule.nextExecutionAt && (
                    <p>
                      <span className="text-muted-foreground">{t('lockey_reporting_next_run')}:</span>{' '}
                      {new Date(schedule.nextExecutionAt).toLocaleString()}
                    </p>
                  )}
                </div>
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={() => deleteSchedule.mutate(schedule.id)}
                  disabled={deleteSchedule.isPending}
                >
                  {t('lockey_reporting_action_delete')}
                </Button>
              </div>
            </CardContent>
          </Card>
        ))}
        {data?.items.length === 0 && (
          <p className="text-muted-foreground">{t('lockey_reporting_no_schedules')}</p>
        )}
      </div>
    </div>
  );
}
