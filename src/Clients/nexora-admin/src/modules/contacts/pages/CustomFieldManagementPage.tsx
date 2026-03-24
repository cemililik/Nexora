import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { Checkbox } from '@/shared/components/ui/checkbox';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { usePagination } from '@/shared/hooks/usePagination';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useApiError } from '@/shared/hooks/useApiError';
import {
  useCustomFieldDefinitions,
  useCreateCustomField,
  useUpdateCustomField,
  useDeleteCustomField,
} from '../hooks/useCustomFields';
import type {
  CustomFieldDefinitionDto,
  CreateCustomFieldRequest,
  UpdateCustomFieldRequest,
} from '../types';

const FIELD_TYPES = ['text', 'number', 'date', 'dropdown', 'boolean'] as const;

function createCustomFieldSchema(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    fieldName: z.string().min(1, t('lockey_validation_required', { ns: 'validation' })),
    fieldType: z.string().min(1, t('lockey_validation_required', { ns: 'validation' })),
    options: z.string().optional(),
    isRequired: z.boolean(),
    displayOrder: z.number().min(0, t('lockey_validation_required', { ns: 'validation' })),
  }).superRefine((data, ctx) => {
    if (data.fieldType === 'dropdown' && (!data.options || !data.options.trim())) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['options'],
        message: t('lockey_contacts_error_dropdown_options_required', { ns: 'contacts' }),
      });
    }
  });
}

type CustomFieldFormValues = z.infer<ReturnType<typeof createCustomFieldSchema>>;

export default function CustomFieldManagementPage() {
  const { t } = useTranslation('contacts');
  const { page, pageSize, setPage } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { handleApiError } = useApiError();
  const { hasPermission } = usePermissions();

  const canManage = hasPermission('contacts.custom-field.manage');

  const { data: definitions, isPending } = useCustomFieldDefinitions();
  const createField = useCreateCustomField();
  const [editingField, setEditingField] = useState<CustomFieldDefinitionDto | null>(null);
  const updateField = useUpdateCustomField(editingField?.id ?? '');
  const deleteField = useDeleteCustomField();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);

  const schema = useMemo(() => createCustomFieldSchema(t), [t]);
  const form = useForm<CustomFieldFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      fieldName: '',
      fieldType: 'text',
      options: '',
      isRequired: false,
      displayOrder: 0,
    },
  });

  const watchedFieldType = useWatch({ control: form.control, name: 'fieldType' });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_contacts_module_name' },
      { label: 'lockey_contacts_custom_fields_title' },
    ]);
  }, [setBreadcrumbs]);

  const openCreateDialog = () => {
    setEditingField(null);
    form.reset({
      fieldName: '',
      fieldType: 'text',
      options: '',
      isRequired: false,
      displayOrder: 0,
    });
    setDialogOpen(true);
  };

  const openEditDialog = useCallback((field: CustomFieldDefinitionDto) => {
    setEditingField(field);
    form.reset({
      fieldName: field.fieldName,
      fieldType: field.fieldType,
      options: field.options ?? '',
      isRequired: field.isRequired,
      displayOrder: field.displayOrder,
    });
    setDialogOpen(true);
  }, [form]);

  const onSubmit = (values: CustomFieldFormValues) => {
    if (editingField) {
      const data: UpdateCustomFieldRequest = {
        fieldName: values.fieldName,
        options: values.fieldType === 'dropdown' ? values.options || undefined : undefined,
        isRequired: values.isRequired,
        displayOrder: values.displayOrder,
      };
      updateField.mutate(data, {
        onSuccess: () => {
          form.reset();
          setDialogOpen(false);
        },
        onError: (err) => handleApiError(err),
      });
    } else {
      const data: CreateCustomFieldRequest = {
        fieldName: values.fieldName,
        fieldType: values.fieldType,
        options: values.fieldType === 'dropdown' ? values.options || undefined : undefined,
        isRequired: values.isRequired,
        displayOrder: values.displayOrder,
      };
      createField.mutate(data, {
        onSuccess: () => {
          form.reset();
          setDialogOpen(false);
        },
        onError: (err) => handleApiError(err),
      });
    }
  };

  // Client-side pagination
  const allDefs = definitions ?? [];
  const totalCount = allDefs.length;
  const paginatedDefs = allDefs.slice((page - 1) * pageSize, page * pageSize);

  const columns: ColumnDef<CustomFieldDefinitionDto>[] = useMemo(() => [
    {
      key: 'fieldName',
      header: t('lockey_contacts_col_field_name'),
      render: (row) => row.fieldName,
    },
    {
      key: 'fieldType',
      header: t('lockey_contacts_col_field_type'),
      render: (row) => t(`lockey_contacts_field_type_${row.fieldType}`),
    },
    {
      key: 'isRequired',
      header: t('lockey_contacts_col_is_required'),
      render: (row) => (
        <Badge variant={row.isRequired ? 'default' : 'secondary'}>
          {row.isRequired
            ? t('lockey_contacts_required')
            : t('lockey_contacts_optional')}
        </Badge>
      ),
    },
    {
      key: 'displayOrder',
      header: t('lockey_contacts_col_display_order'),
      render: (row) => String(row.displayOrder),
    },
    {
      key: 'isActive',
      header: t('lockey_contacts_custom_fields_col_active'),
      render: (row) => (
        <Badge variant={row.isActive ? 'default' : 'secondary'}>
          {row.isActive
            ? t('lockey_contacts_active')
            : t('lockey_contacts_inactive')}
        </Badge>
      ),
    },
    {
      key: 'actions',
      header: t('lockey_contacts_col_actions'),
      render: (row) => (
        <div className="flex gap-1">
          {canManage && (
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => openEditDialog(row)}
            >
              {t('lockey_contacts_action_edit')}
            </Button>
          )}
          {canManage && (
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setDeleteConfirmId(row.id)}
            >
              {t('lockey_common_delete', { ns: 'common' })}
            </Button>
          )}
        </div>
      ),
    },
  ], [t, canManage, openEditDialog]);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('lockey_contacts_custom_fields_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_contacts_custom_fields_description')}
          </p>
        </div>
        {canManage && (
          <Button type="button" onClick={openCreateDialog}>
            {t('lockey_contacts_create_custom_field')}
          </Button>
        )}
      </div>

      <DataTable
        columns={columns}
        data={paginatedDefs}
        totalCount={totalCount}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        isLoading={isPending}
        emptyMessage={t('lockey_contacts_empty_custom_fields')}
        keyExtractor={(row) => row.id}
      />

      {/* Create/Edit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {editingField
                ? t('lockey_contacts_edit_custom_field')
                : t('lockey_contacts_create_custom_field')}
            </DialogTitle>
            <DialogDescription className="sr-only">
              {editingField
                ? t('lockey_contacts_edit_custom_field')
                : t('lockey_contacts_create_custom_field')}
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <div>
              <label htmlFor="cf-fieldName" className="text-sm font-medium">{t('lockey_contacts_col_field_name')}</label>
              <input
                id="cf-fieldName"
                type="text"
                {...form.register('fieldName')}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
              {form.formState.errors.fieldName?.message && (
                <p className="mt-1 text-sm text-destructive">
                  {form.formState.errors.fieldName.message}
                </p>
              )}
            </div>
            {!editingField && (
              <div>
                <label htmlFor="cf-fieldType" className="text-sm font-medium">{t('lockey_contacts_col_field_type')}</label>
                <Controller
                  control={form.control}
                  name="fieldType"
                  render={({ field }) => (
                    <Select value={field.value} onValueChange={field.onChange}>
                      <SelectTrigger id="cf-fieldType" className="mt-1">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {FIELD_TYPES.map((ft) => (
                          <SelectItem key={ft} value={ft}>
                            {t(`lockey_contacts_field_type_${ft}`)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                />
                {form.formState.errors.fieldType?.message && (
                  <p className="mt-1 text-sm text-destructive">
                    {form.formState.errors.fieldType.message}
                  </p>
                )}
              </div>
            )}
            {watchedFieldType === 'dropdown' && (
              <div>
                <label htmlFor="cf-options" className="text-sm font-medium">{t('lockey_contacts_field_options')}</label>
                <textarea
                  id="cf-options"
                  {...form.register('options')}
                  placeholder={t('lockey_contacts_field_options_placeholder')}
                  className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                  rows={3}
                />
                {form.formState.errors.options?.message && (
                  <p className="mt-1 text-sm text-destructive">
                    {form.formState.errors.options.message}
                  </p>
                )}
              </div>
            )}
            <Controller
              control={form.control}
              name="isRequired"
              render={({ field }) => (
                <div className="flex items-center gap-2">
                  <Checkbox
                    id="isRequired"
                    checked={!!field.value}
                    onCheckedChange={(checked) => field.onChange(checked === true)}
                  />
                  <label htmlFor="isRequired" className="text-sm font-medium">
                    {t('lockey_contacts_col_is_required')}
                  </label>
                </div>
              )}
            />
            <div>
              <label htmlFor="cf-displayOrder" className="text-sm font-medium">{t('lockey_contacts_col_display_order')}</label>
              <input
                id="cf-displayOrder"
                type="number"
                {...form.register('displayOrder', { valueAsNumber: true })}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                min={0}
              />
              {form.formState.errors.displayOrder?.message && (
                <p className="mt-1 text-sm text-destructive">
                  {form.formState.errors.displayOrder.message}
                </p>
              )}
            </div>
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                onClick={() => setDialogOpen(false)}
              >
                {t('lockey_common_cancel', { ns: 'common' })}
              </Button>
              <Button
                type="submit"
                disabled={createField.isPending || updateField.isPending}
              >
                {editingField
                  ? t('lockey_common_save', { ns: 'common' })
                  : t('lockey_contacts_create_custom_field')}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Delete Confirm */}
      <ConfirmDialog
        open={deleteConfirmId !== null}
        onOpenChange={() => setDeleteConfirmId(null)}
        title={t('lockey_contacts_delete_custom_field_title')}
        description={t('lockey_contacts_delete_custom_field_description')}
        variant="destructive"
        onConfirm={() => {
          if (deleteConfirmId) {
            deleteField.mutate(deleteConfirmId, { onError: (err) => handleApiError(err) });
          }
          setDeleteConfirmId(null);
        }}
        isPending={deleteField.isPending}
      />
    </div>
  );
}
