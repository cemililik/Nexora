import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { toast } from 'sonner';

import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { useApiError } from '@/shared/hooks/useApiError';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import {
  useTemplate,
  useCreateTemplate,
  useUpdateTemplate,
  useActivateTemplate,
  useDeactivateTemplate,
  useRenderTemplate,
} from '../hooks/useTemplates';
import { useFolders } from '../hooks/useFolders';

const CATEGORIES = ['Contract', 'Receipt', 'Letter', 'Report'] as const;
const FORMATS = ['Docx', 'Pdf', 'Html'] as const;

function createTemplateSchema(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    name: z.string().min(1, t('lockey_validation_required', { ns: 'validation' })),
    category: z.enum([...CATEGORIES], { message: t('lockey_validation_required', { ns: 'validation' }) }),
    format: z.enum([...FORMATS], { message: t('lockey_validation_required', { ns: 'validation' }) }),
    templateStorageKey: z.string().min(1, t('lockey_validation_required', { ns: 'validation' })),
    variableDefinitions: z.string().optional(),
  });
}

type TemplateFormValues = z.infer<ReturnType<typeof createTemplateSchema>>;

export default function TemplateDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t, i18n } = useTranslation('documents');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const canManage = hasPermission('documents.template.manage');
  const { handleApiError } = useApiError();

  const isCreate = id === 'create';
  const { data: template, isPending } = useTemplate(isCreate ? '' : (id ?? ''));
  const createTemplate = useCreateTemplate();
  const updateTemplate = useUpdateTemplate(id ?? '');
  const activateTemplate = useActivateTemplate();
  const deactivateTemplate = useDeactivateTemplate();
  const renderTemplate = useRenderTemplate(id ?? '');
  const { data: folders } = useFolders();

  const [renderOpen, setRenderOpen] = useState(false);
  const [renderFolderId, setRenderFolderId] = useState('');
  const [renderOutputName, setRenderOutputName] = useState('');
  const [renderVariables, setRenderVariables] = useState('{}');

  const schema = createTemplateSchema(t);
  const form = useForm<TemplateFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: '',
      category: 'Contract',
      format: 'Pdf',
      templateStorageKey: '',
      variableDefinitions: '',
    },
  });

  useEffect(() => {
    if (template && !isCreate) {
      form.reset({
        name: template.name,
        category: template.category,
        format: template.format,
        templateStorageKey: template.templateStorageKey,
        variableDefinitions: template.variableDefinitions ?? '',
      });
    }
  }, [template, isCreate, form]);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_documents_module_name' },
      { label: 'lockey_documents_templates_title' },
      { label: isCreate ? t('lockey_documents_templates_create') : (template?.name ?? '...') },
    ]);
  }, [setBreadcrumbs, isCreate, template?.name, t]);

  if (!isCreate && isPending) return <LoadingSkeleton />;

  const onSubmit = (values: TemplateFormValues) => {
    if (isCreate) {
      createTemplate.mutate(
        {
          name: values.name,
          category: values.category,
          format: values.format,
          templateStorageKey: values.templateStorageKey,
          variableDefinitions: values.variableDefinitions || undefined,
        },
        { onSuccess: () => navigate('/documents/templates'), onError: (err) => handleApiError(err) },
      );
    } else {
      updateTemplate.mutate(
        {
          name: values.name,
          category: values.category,
          format: values.format,
          variableDefinitions: values.variableDefinitions || undefined,
        },
        { onSuccess: () => navigate('/documents/templates'), onError: (err) => handleApiError(err) },
      );
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">
          {isCreate ? t('lockey_documents_templates_create') : t('lockey_documents_templates_edit')}
        </h1>
        {!isCreate && canManage && template && (
          <div className="flex gap-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => {
                if (template.isActive) {
                  deactivateTemplate.mutate(id!, { onError: (err) => handleApiError(err) });
                } else {
                  activateTemplate.mutate(id!, { onError: (err) => handleApiError(err) });
                }
              }}
            >
              {template.isActive
                ? t('lockey_documents_templates_deactivate')
                : t('lockey_documents_templates_activate')}
            </Button>
            <Button type="button" variant="outline" onClick={() => setRenderOpen(true)}>
              {t('lockey_documents_templates_render')}
            </Button>
          </div>
        )}
      </div>

      {!isCreate && template && (
        <div className="flex items-center gap-2">
          <Badge variant={template.isActive ? 'default' : 'secondary'}>
            {template.isActive ? t('lockey_documents_status_active') : t('lockey_documents_status_archived')}
          </Badge>
          <span className="text-sm text-muted-foreground">
            {new Date(template.createdAt).toLocaleDateString(i18n.language)}
          </span>
        </div>
      )}

      <form onSubmit={form.handleSubmit(onSubmit)} className="max-w-2xl space-y-4">
        <div>
          <label className="text-sm font-medium">{t('lockey_documents_templates_form_name')}</label>
          <input
            type="text"
            {...form.register('name')}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
          />
          {form.formState.errors.name?.message && (
            <p className="mt-1 text-sm text-destructive">{form.formState.errors.name.message}</p>
          )}
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="text-sm font-medium">{t('lockey_documents_templates_form_category')}</label>
            <Select
              value={form.watch('category')}
              onValueChange={(v) => form.setValue('category', v as typeof CATEGORIES[number])}
            >
              <SelectTrigger className="mt-1" aria-label={t('lockey_documents_templates_form_category')}>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {CATEGORIES.map((c) => (
                  <SelectItem key={c} value={c}>
                    {t(`lockey_documents_templates_category_${c.toLowerCase()}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div>
            <label className="text-sm font-medium">{t('lockey_documents_templates_form_format')}</label>
            <Select
              value={form.watch('format')}
              onValueChange={(v) => form.setValue('format', v as typeof FORMATS[number])}
            >
              <SelectTrigger className="mt-1" aria-label={t('lockey_documents_templates_form_format')}>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {FORMATS.map((f) => (
                  <SelectItem key={f} value={f}>
                    {t(`lockey_documents_templates_format_${f.toLowerCase()}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>

        {isCreate && (
          <div>
            <label className="text-sm font-medium">{t('lockey_documents_templates_form_storage_key')}</label>
            <input
              type="text"
              {...form.register('templateStorageKey')}
              className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            />
            {form.formState.errors.templateStorageKey?.message && (
              <p className="mt-1 text-sm text-destructive">
                {form.formState.errors.templateStorageKey.message}
              </p>
            )}
          </div>
        )}

        <div>
          <label className="text-sm font-medium">{t('lockey_documents_templates_form_variables')}</label>
          <textarea
            {...form.register('variableDefinitions')}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm font-mono"
            rows={4}
          />
        </div>

        <div className="flex gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() => navigate('/documents/templates')}
          >
            {t('lockey_common_cancel', { ns: 'common' })}
          </Button>
          <Button
            type="submit"
            disabled={createTemplate.isPending || updateTemplate.isPending}
          >
            {isCreate
              ? t('lockey_documents_templates_create')
              : t('lockey_common_save', { ns: 'common' })}
          </Button>
        </div>
      </form>

      {/* Render Dialog */}
      <Dialog open={renderOpen} onOpenChange={setRenderOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('lockey_documents_templates_render')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <label className="text-sm font-medium">{t('lockey_documents_templates_render_form_folder')}</label>
              <Select value={renderFolderId} onValueChange={setRenderFolderId}>
                <SelectTrigger className="mt-1" aria-label={t('lockey_documents_templates_render_form_folder')}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {folders?.map((f) => (
                    <SelectItem key={f.id} value={f.id}>
                      {f.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_documents_templates_render_form_name')}</label>
              <input
                type="text"
                value={renderOutputName}
                onChange={(e) => setRenderOutputName(e.target.value)}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_documents_templates_render_form_variables')}</label>
              <textarea
                value={renderVariables}
                onChange={(e) => setRenderVariables(e.target.value)}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm font-mono"
                rows={4}
              />
            </div>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setRenderOpen(false)}>
              {t('lockey_common_cancel', { ns: 'common' })}
            </Button>
            <Button
              type="button"
              disabled={renderTemplate.isPending || !renderFolderId || !renderOutputName}
              onClick={() => {
                let variables: Record<string, string> = {};
                try {
                  variables = JSON.parse(renderVariables);
                } catch {
                  toast.error(t('lockey_validation_invalid_json', { ns: 'validation' }));
                  return;
                }
                renderTemplate.mutate(
                  { folderId: renderFolderId, outputName: renderOutputName, variables },
                  { onSuccess: () => setRenderOpen(false), onError: (err) => handleApiError(err) },
                );
              }}
            >
              {t('lockey_documents_templates_render')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
