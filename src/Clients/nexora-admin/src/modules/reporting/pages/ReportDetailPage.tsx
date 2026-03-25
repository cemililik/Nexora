import { useState, useCallback, useMemo, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams, useNavigate } from 'react-router';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Download, Eye, Pencil, Trash2 } from 'lucide-react';

import { usePermissions } from '@/shared/hooks/usePermissions';
import { useApiError } from '@/shared/hooks/useApiError';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import {
  useReportDefinition,
  useUpdateReportDefinition,
  useDeleteReportDefinition,
  useTestReportQuery,
} from '../hooks/useReportDefinitions';
import { SqlEditor } from '../components/SqlEditor';
import { SqlTestResult } from '../components/SqlTestResult';
import { useReportExecutions, useExecuteReport, useReportFile } from '../hooks/useReportExecutions';
import { ReportStatusBadge } from '../components/ReportStatusBadge';
import { ReportParameterForm } from '../components/ReportParameterForm';
import type { ReportFormat } from '../types';

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const PREVIEWABLE_FORMATS = new Set(['Pdf', 'Json', 'Csv']);
const FORMATS = ['Csv', 'Excel', 'Pdf', 'Json'] as const;

export default function ReportDetailPage() {
  const { t } = useTranslation('reporting');
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const isValidId = !!id && UUID_RE.test(id);
  const { data: definition, isLoading } = useReportDefinition(isValidId ? id : '');
  const { data: executions } = useReportExecutions({
    definitionId: isValidId ? id : undefined,
    page: 1,
    pageSize: 10,
  });
  const { hasPermission } = usePermissions();
  const { handleApiError } = useApiError();
  const executeReport = useExecuteReport();
  const updateDefinition = useUpdateReportDefinition();
  const deleteDefinition = useDeleteReportDefinition();
  const reportFile = useReportFile();

  const [paramValues, setParamValues] = useState<Record<string, string>>({});
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [previewFormat, setPreviewFormat] = useState<string>('');
  const [previewText, setPreviewText] = useState<string | null>(null);
  const [editOpen, setEditOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  const handleDownload = useCallback((executionId: string, format: string) => {
    reportFile.mutate(executionId, {
      onSuccess: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `report-${executionId}.${formatToExtension(format)}`;
        link.click();
        URL.revokeObjectURL(url);
      },
      onError: (err) => handleApiError(err),
    });
  }, [reportFile, handleApiError]);

  const handlePreview = useCallback((executionId: string, format: string) => {
    reportFile.mutate(executionId, {
      onSuccess: async (blob) => {
        setPreviewFormat(format);
        if (format === 'Pdf') {
          setPreviewUrl(URL.createObjectURL(blob));
          setPreviewText(null);
        } else {
          setPreviewText(await blob.text());
          setPreviewUrl(null);
        }
      },
      onError: (err) => handleApiError(err),
    });
  }, [reportFile, handleApiError]);

  const closePreview = useCallback(() => {
    if (previewUrl) URL.revokeObjectURL(previewUrl);
    setPreviewUrl(null);
    setPreviewText(null);
  }, [previewUrl]);

  const handleDelete = () => {
    if (!definition) return;
    deleteDefinition.mutate(definition.id, {
      onSuccess: () => {
        setDeleteOpen(false);
        void navigate('/reporting/reports');
      },
      onError: (err) => handleApiError(err),
    });
  };

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
    }, {
      onError: (err) => handleApiError(err),
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
        <div className="flex items-center gap-2">
          {hasPermission('reporting.definition.manage') && (
            <Button variant="outline" size="icon" title={t('lockey_reporting_action_edit')} onClick={() => setEditOpen(true)}>
              <Pencil className="h-4 w-4" />
            </Button>
          )}
          {hasPermission('reporting.definition.manage') && (
            <Button variant="outline" size="icon" title={t('lockey_reporting_action_delete')} onClick={() => setDeleteOpen(true)}>
              <Trash2 className="h-4 w-4 text-destructive" />
            </Button>
          )}
          <Button onClick={() => handleExecute()} disabled={executeReport.isPending}>
            {t('lockey_reporting_action_execute')}
          </Button>
        </div>
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
                <p className="text-sm text-muted-foreground">{t('lockey_reporting_no_executions')}</p>
              ) : (
                <div className="space-y-2">
                  {executions?.items.map((exec) => (
                    <div key={exec.id} className="flex items-center justify-between rounded-md border border-border p-3">
                      <div className="flex items-center gap-3">
                        <ReportStatusBadge status={exec.status} />
                        <span className="text-sm text-muted-foreground">
                          {new Date(exec.createdAt).toLocaleString()}
                        </span>
                      </div>
                      <div className="flex items-center gap-2 text-sm text-muted-foreground">
                        {exec.rowCount != null && <span>{t('lockey_reporting_row_count', { count: exec.rowCount })}</span>}
                        {exec.durationMs != null && <span>{t('lockey_reporting_duration', { ms: exec.durationMs })}</span>}
                        <span>{exec.format}</span>
                        {exec.status === 'Completed' && (
                          <>
                            {PREVIEWABLE_FORMATS.has(exec.format) && (
                              <Button variant="ghost" size="icon" className="h-7 w-7" title={t('lockey_reporting_action_preview')} disabled={reportFile.isPending} onClick={() => handlePreview(exec.id, exec.format)}>
                                <Eye className="h-4 w-4" />
                              </Button>
                            )}
                            <Button variant="ghost" size="icon" className="h-7 w-7" title={t('lockey_reporting_action_download')} disabled={reportFile.isPending} onClick={() => handleDownload(exec.id, exec.format)}>
                              <Download className="h-4 w-4" />
                            </Button>
                          </>
                        )}
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
                  {definition.isActive ? t('lockey_reporting_active') : t('lockey_reporting_inactive')}
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

      {/* Preview dialog */}
      <Dialog open={!!(previewUrl || previewText)} onOpenChange={(open) => { if (!open) closePreview(); }}>
        <DialogContent className="max-w-4xl h-[80vh] flex flex-col gap-0 p-0">
          <DialogHeader className="px-6 pt-6 pb-3">
            <DialogTitle>{t('lockey_reporting_action_preview')}</DialogTitle>
          </DialogHeader>
          {previewUrl && previewFormat === 'Pdf' && (
            <iframe src={previewUrl} className="flex-1 w-full border-0 rounded-b-lg" title={t('lockey_reporting_action_preview')} />
          )}
          {previewText != null && (
            <pre className="flex-1 overflow-auto mx-6 mb-6 rounded-md bg-muted p-4 text-sm whitespace-pre-wrap">{previewText}</pre>
          )}
        </DialogContent>
      </Dialog>

      {/* Edit dialog */}
      <EditReportDialog
        open={editOpen}
        onOpenChange={setEditOpen}
        definition={definition}
        onSave={(values) => {
          updateDefinition.mutate(
            { id: definition.id, ...values },
            { onSuccess: () => setEditOpen(false), onError: (err) => handleApiError(err) },
          );
        }}
        isPending={updateDefinition.isPending}
      />

      {/* Delete confirmation dialog */}
      <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle>{t('lockey_reporting_action_delete')}</DialogTitle>
            <DialogDescription>{t('lockey_reporting_confirm_delete')}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteOpen(false)}>
              {t('lockey_reporting_cancel')}
            </Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleteDefinition.isPending}>
              {t('lockey_reporting_action_delete')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

/* ── Edit Dialog ── */

interface EditReportDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  definition: { name: string; description?: string; module: string; category?: string; queryText: string; parameters?: string; defaultFormat: string };
  onSave: (values: { name: string; description?: string; module: string; category?: string; queryText: string; parameters?: string; defaultFormat: string }) => void;
  isPending: boolean;
}

function EditReportDialog({ open, onOpenChange, definition, onSave, isPending }: EditReportDialogProps) {
  const { t } = useTranslation('reporting');
  const testQuery = useTestReportQuery();

  const schema = useMemo(() => z.object({
    name: z.string().min(1, t('lockey_reporting_validation_name_required')),
    description: z.string().optional(),
    module: z.string().min(1, t('lockey_reporting_validation_module_required')),
    category: z.string().optional(),
    queryText: z.string().min(1, t('lockey_reporting_validation_query_required')),
    parameters: z.string().optional(),
    defaultFormat: z.enum([...FORMATS], { message: t('lockey_reporting_validation_format_required') }),
  }), [t]);

  type FormValues = z.infer<typeof schema>;

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: definition.name,
      description: definition.description ?? '',
      module: definition.module,
      category: definition.category ?? '',
      queryText: definition.queryText,
      parameters: definition.parameters ?? '',
      defaultFormat: definition.defaultFormat as typeof FORMATS[number],
    },
  });

  useEffect(() => {
    if (open) {
      form.reset({
        name: definition.name,
        description: definition.description ?? '',
        module: definition.module,
        category: definition.category ?? '',
        queryText: definition.queryText,
        parameters: definition.parameters ?? '',
        defaultFormat: definition.defaultFormat as typeof FORMATS[number],
      });
    }
  }, [open, definition, form]);

  const onSubmit = (values: FormValues) => {
    onSave({
      name: values.name,
      description: values.description || undefined,
      module: values.module,
      category: values.category || undefined,
      queryText: values.queryText,
      parameters: values.parameters || undefined,
      defaultFormat: values.defaultFormat,
    });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{t('lockey_reporting_action_edit')}</DialogTitle>
          <DialogDescription className="sr-only">{t('lockey_reporting_action_edit')}</DialogDescription>
        </DialogHeader>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
          <div className="space-y-2">
            <label htmlFor="edit-name" className="text-sm font-medium">{t('lockey_reporting_col_name')}</label>
            <Input id="edit-name" {...form.register('name')} />
            {form.formState.errors.name && <p className="text-sm text-destructive">{form.formState.errors.name.message}</p>}
          </div>
          <div className="space-y-2">
            <label htmlFor="edit-desc" className="text-sm font-medium">{t('lockey_reporting_col_description')}</label>
            <Input id="edit-desc" {...form.register('description')} />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <label htmlFor="edit-module" className="text-sm font-medium">{t('lockey_reporting_col_module')}</label>
              <Input id="edit-module" {...form.register('module')} />
              {form.formState.errors.module && <p className="text-sm text-destructive">{form.formState.errors.module.message}</p>}
            </div>
            <div className="space-y-2">
              <label htmlFor="edit-format" className="text-sm font-medium">{t('lockey_reporting_col_format')}</label>
              <Controller
                control={form.control}
                name="defaultFormat"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="edit-format" aria-label={t('lockey_reporting_col_format')}>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {FORMATS.map((f) => <SelectItem key={f} value={f}>{f}</SelectItem>)}
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
          </div>
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <label className="text-sm font-medium">{t('lockey_reporting_query')}</label>
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={testQuery.isPending || !form.watch('queryText')}
                onClick={() => testQuery.mutate(form.getValues('queryText'))}
              >
                {t('lockey_reporting_action_test_query')}
              </Button>
            </div>
            <Controller
              control={form.control}
              name="queryText"
              render={({ field }) => (
                <SqlEditor
                  value={field.value}
                  onChange={field.onChange}
                />
              )}
            />
            {form.formState.errors.queryText && <p className="text-sm text-destructive">{form.formState.errors.queryText.message}</p>}
            <SqlTestResult result={testQuery.data} isPending={testQuery.isPending} />
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              {t('lockey_reporting_cancel')}
            </Button>
            <Button type="submit" disabled={isPending}>{t('lockey_reporting_action_save')}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function formatToExtension(format: string): string {
  switch (format) {
    case 'Pdf': return 'pdf';
    case 'Csv': return 'csv';
    case 'Json': return 'json';
    case 'Excel': return 'xlsx';
    default: return 'bin';
  }
}
