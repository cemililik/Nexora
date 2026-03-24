import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';

import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { useReportDefinition } from '../hooks/useReportDefinitions';
import { useReportExecutions, useExecuteReport } from '../hooks/useReportExecutions';
import { ReportStatusBadge } from '../components/ReportStatusBadge';
import { ReportParameterForm } from '../components/ReportParameterForm';
import type { ReportFormat } from '../types';

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export default function ReportDetailPage() {
  const { t } = useTranslation('reporting');
  const { id } = useParams<{ id: string }>();
  const isValidId = !!id && UUID_RE.test(id);
  const { data: definition, isLoading } = useReportDefinition(isValidId ? id : '');
  const { data: executions } = useReportExecutions({
    definitionId: isValidId ? id : undefined,
    page: 1,
    pageSize: 10,
  });
  const executeReport = useExecuteReport();
  const [paramValues, setParamValues] = useState<Record<string, string>>({});

  if (isLoading || !definition) {
    return <p className="text-muted-foreground">{t('lockey_reporting_loading')}</p>;
  }

  const parameters = definition.parameters
    ? (JSON.parse(definition.parameters) as Array<{ name: string; type: string; required: boolean; defaultValue?: string }>)
    : [];

  const handleExecute = (format?: ReportFormat) => {
    executeReport.mutate({
      definitionId: definition.id,
      format,
      parameterValues: Object.keys(paramValues).length > 0
        ? JSON.stringify(paramValues)
        : undefined,
    });
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-foreground">{definition.name}</h1>
          {definition.description && (
            <p className="mt-1 text-muted-foreground">{definition.description}</p>
          )}
        </div>
        <Button onClick={() => handleExecute()} disabled={executeReport.isPending}>
          {t('lockey_reporting_action_execute')}
        </Button>
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2 space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">{t('lockey_reporting_query')}</CardTitle>
            </CardHeader>
            <CardContent>
              <pre className="overflow-auto rounded-md bg-muted p-4 text-sm">
                {definition.queryText}
              </pre>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">{t('lockey_reporting_execution_history')}</CardTitle>
            </CardHeader>
            <CardContent>
              {executions?.items.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  {t('lockey_reporting_no_executions')}
                </p>
              ) : (
                <div className="space-y-2">
                  {executions?.items.map((exec) => (
                    <div
                      key={exec.id}
                      className="flex items-center justify-between rounded-md border border-border p-3"
                    >
                      <div className="flex items-center gap-3">
                        <ReportStatusBadge status={exec.status} />
                        <span className="text-sm text-muted-foreground">
                          {new Date(exec.createdAt).toLocaleString()}
                        </span>
                      </div>
                      <div className="flex items-center gap-2 text-sm text-muted-foreground">
                        {exec.rowCount != null && <span>{exec.rowCount} rows</span>}
                        {exec.durationMs != null && <span>{exec.durationMs}ms</span>}
                        <span>{exec.format}</span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">{t('lockey_reporting_details')}</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-muted-foreground">{t('lockey_reporting_col_module')}</span>
                <span className="text-foreground">{definition.module}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">{t('lockey_reporting_col_format')}</span>
                <span className="text-foreground">{definition.defaultFormat}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">{t('lockey_reporting_col_status')}</span>
                <span className="text-foreground">
                  {definition.isActive
                    ? t('lockey_reporting_active')
                    : t('lockey_reporting_inactive')}
                </span>
              </div>
            </CardContent>
          </Card>

          {parameters.length > 0 && (
            <Card>
              <CardContent className="pt-6">
                <ReportParameterForm
                  parameters={parameters}
                  values={paramValues}
                  onChange={setParamValues}
                  onSubmit={() => handleExecute()}
                  isLoading={executeReport.isPending}
                />
              </CardContent>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}
