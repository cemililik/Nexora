import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams, Link } from 'react-router';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Textarea } from '@/shared/components/ui/textarea';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
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
import { useReportDefinitions, useCreateReportDefinition, useTestReportQuery } from '../hooks/useReportDefinitions';
import { SqlEditor } from '../components/SqlEditor';
import { SqlTestResult } from '../components/SqlTestResult';

const FORMATS = ['Csv', 'Excel', 'Pdf', 'Json'] as const;

function createSchema(t: (key: string) => string) {
  return z.object({
    name: z.string().min(1, t('lockey_reporting_validation_name_required')),
    description: z.string().optional(),
    module: z.string().min(1, t('lockey_reporting_validation_module_required')),
    category: z.string().optional(),
    queryText: z.string().min(1, t('lockey_reporting_validation_query_required')),
    parameters: z.string().optional(),
    defaultFormat: z.enum([...FORMATS], {
      message: t('lockey_reporting_validation_format_required'),
    }),
  });
}

type FormValues = z.infer<ReturnType<typeof createSchema>>;

export default function ReportListPage() {
  const { t } = useTranslation('reporting');
  const [searchParams, setSearchParams] = useSearchParams();
  const [dialogOpen, setDialogOpen] = useState(false);

  const page = Number(searchParams.get('page') ?? '1');
  const search = searchParams.get('search') ?? '';

  const { data, isLoading } = useReportDefinitions({ page, pageSize: 20, search: search || undefined });
  const createDefinition = useCreateReportDefinition();
  const testQuery = useTestReportQuery();

  const schema = useMemo(() => createSchema(t), [t]);

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: '',
      description: '',
      module: '',
      category: '',
      queryText: '',
      parameters: '',
      defaultFormat: 'Csv',
    },
  });

  const onSubmit = (values: FormValues) => {
    createDefinition.mutate(
      {
        name: values.name,
        description: values.description || undefined,
        module: values.module,
        category: values.category || undefined,
        queryText: values.queryText,
        parameters: values.parameters || undefined,
        defaultFormat: values.defaultFormat,
      },
      {
        onSuccess: () => {
          setDialogOpen(false);
          form.reset();
        },
      },
    );
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-foreground">
          {t('lockey_reporting_nav_reports')}
        </h1>
        <Button onClick={() => setDialogOpen(true)}>
          {t('lockey_reporting_action_create_definition')}
        </Button>
      </div>

      <Input
        placeholder={t('lockey_reporting_search_placeholder')}
        value={search}
        onChange={(e) => {
          const params = new URLSearchParams(searchParams);
          params.set('search', e.target.value);
          params.set('page', '1');
          setSearchParams(params);
        }}
        className="max-w-sm"
      />

      {isLoading && (
        <p className="text-muted-foreground">{t('lockey_reporting_loading')}</p>
      )}

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {data?.items.map((def) => (
          <Link key={def.id} to={`/reporting/reports/${def.id}`}>
            <Card className="transition-colors hover:border-primary">
              <CardHeader className="pb-2">
                <CardTitle className="text-base">{def.name}</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground line-clamp-2">
                  {def.description ?? t('lockey_reporting_no_description')}
                </p>
                <div className="mt-2 flex items-center gap-2 text-xs text-muted-foreground">
                  <span>{def.module}</span>
                  <span>·</span>
                  <span>{def.defaultFormat}</span>
                  {!def.isActive && (
                    <>
                      <span>·</span>
                      <span className="text-destructive">{t('lockey_reporting_inactive')}</span>
                    </>
                  )}
                </div>
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>

      {data && data.totalPages > 1 && (
        <div className="flex justify-center gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={!data.hasPreviousPage}
            onClick={() => {
              const params = new URLSearchParams(searchParams);
              params.set('page', String(page - 1));
              setSearchParams(params);
            }}
          >
            {t('lockey_reporting_prev')}
          </Button>
          <span className="flex items-center text-sm text-muted-foreground">
            {page} / {data.totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={!data.hasNextPage}
            onClick={() => {
              const params = new URLSearchParams(searchParams);
              params.set('page', String(page + 1));
              setSearchParams(params);
            }}
          >
            {t('lockey_reporting_next')}
          </Button>
        </div>
      )}

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{t('lockey_reporting_action_create_definition')}</DialogTitle>
            <DialogDescription className="sr-only">
              {t('lockey_reporting_action_create_definition')}
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <div className="space-y-2">
              <label htmlFor="rd-name" className="text-sm font-medium">
                {t('lockey_reporting_col_name')}
              </label>
              <Input
                id="rd-name"
                {...form.register('name')}
                placeholder={t('lockey_reporting_col_name')}
              />
              {form.formState.errors.name && (
                <p className="text-sm text-destructive">{form.formState.errors.name.message}</p>
              )}
            </div>

            <div className="space-y-2">
              <label htmlFor="rd-desc" className="text-sm font-medium">
                {t('lockey_reporting_col_description')}
              </label>
              <Textarea
                id="rd-desc"
                {...form.register('description')}
                rows={2}
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <label htmlFor="rd-module" className="text-sm font-medium">
                  {t('lockey_reporting_col_module')}
                </label>
                <Input
                  id="rd-module"
                  {...form.register('module')}
                  placeholder="contacts"
                />
                {form.formState.errors.module && (
                  <p className="text-sm text-destructive">{form.formState.errors.module.message}</p>
                )}
              </div>

              <div className="space-y-2">
                <label htmlFor="rd-format" className="text-sm font-medium">
                  {t('lockey_reporting_col_format')}
                </label>
                <Controller
                  control={form.control}
                  name="defaultFormat"
                  render={({ field }) => (
                    <Select value={field.value} onValueChange={field.onChange}>
                      <SelectTrigger id="rd-format" aria-label={t('lockey_reporting_col_format')}>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {FORMATS.map((f) => (
                          <SelectItem key={f} value={f}>{f}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                />
              </div>
            </div>

            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <label className="text-sm font-medium">
                  {t('lockey_reporting_query')}
                </label>
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
                    placeholder="SELECT id, name FROM contacts_contacts"
                  />
                )}
              />
              {form.formState.errors.queryText && (
                <p className="text-sm text-destructive">{form.formState.errors.queryText.message}</p>
              )}
              <SqlTestResult result={testQuery.data} isPending={testQuery.isPending} />
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setDialogOpen(false)}>
                {t('lockey_reporting_cancel')}
              </Button>
              <Button type="submit" disabled={createDefinition.isPending}>
                {t('lockey_reporting_action_create_definition')}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
