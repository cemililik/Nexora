import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
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
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { usePagination } from '@/shared/hooks/usePagination';
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

export default function CustomFieldManagementPage() {
  const { t } = useTranslation('contacts');
  const { page, pageSize, setPage } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { handleApiError } = useApiError();

  const { data: definitions, isPending } = useCustomFieldDefinitions();
  const createField = useCreateCustomField();
  const [editingField, setEditingField] = useState<CustomFieldDefinitionDto | null>(null);
  const updateField = useUpdateCustomField(editingField?.id ?? '');
  const deleteField = useDeleteCustomField();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);

  // Form state
  const [formName, setFormName] = useState('');
  const [formType, setFormType] = useState<string>('text');
  const [formOptions, setFormOptions] = useState('');
  const [formRequired, setFormRequired] = useState(false);
  const [formOrder, setFormOrder] = useState(0);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_contacts_module_name' },
      { label: 'lockey_contacts_custom_fields_title' },
    ]);
  }, [setBreadcrumbs]);

  const openCreateDialog = () => {
    setEditingField(null);
    setFormName('');
    setFormType('text');
    setFormOptions('');
    setFormRequired(false);
    setFormOrder(0);
    setDialogOpen(true);
  };

  const openEditDialog = (field: CustomFieldDefinitionDto) => {
    setEditingField(field);
    setFormName(field.fieldName);
    setFormType(field.fieldType);
    setFormOptions(field.options ?? '');
    setFormRequired(field.isRequired);
    setFormOrder(field.displayOrder);
    setDialogOpen(true);
  };

  const handleSubmit = () => {
    if (formType === 'dropdown' && !formOptions.trim()) {
      toast.error(t('lockey_contacts_error_dropdown_options_required'));
      return;
    }

    if (editingField) {
      const data: UpdateCustomFieldRequest = {
        fieldName: formName,
        options: formType === 'dropdown' ? formOptions || undefined : undefined,
        isRequired: formRequired,
        displayOrder: formOrder,
      };
      updateField.mutate(data, {
        onSuccess: () => setDialogOpen(false),
        onError: (err) => handleApiError(err),
      });
    } else {
      const data: CreateCustomFieldRequest = {
        fieldName: formName,
        fieldType: formType,
        options: formType === 'dropdown' ? formOptions || undefined : undefined,
        isRequired: formRequired,
        displayOrder: formOrder,
      };
      createField.mutate(data, {
        onSuccess: () => setDialogOpen(false),
        onError: (err) => handleApiError(err),
      });
    }
  };

  // Client-side pagination
  const allDefs = definitions ?? [];
  const totalCount = allDefs.length;
  const paginatedDefs = allDefs.slice((page - 1) * pageSize, page * pageSize);

  const columns: ColumnDef<CustomFieldDefinitionDto>[] = [
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
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => openEditDialog(row)}
          >
            {t('lockey_contacts_action_edit')}
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => setDeleteConfirmId(row.id)}
          >
            {t('lockey_common_delete', { ns: 'common' })}
          </Button>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('lockey_contacts_custom_fields_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_contacts_custom_fields_description')}
          </p>
        </div>
        <Button type="button" onClick={openCreateDialog}>
          {t('lockey_contacts_create_custom_field')}
        </Button>
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
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <label className="text-sm font-medium">{t('lockey_contacts_col_field_name')}</label>
              <input
                type="text"
                value={formName}
                onChange={(e) => setFormName(e.target.value)}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
            </div>
            {!editingField && (
              <div>
                <label className="text-sm font-medium">{t('lockey_contacts_col_field_type')}</label>
                <select
                  value={formType}
                  onChange={(e) => setFormType(e.target.value)}
                  className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                >
                  {FIELD_TYPES.map((ft) => (
                    <option key={ft} value={ft}>
                      {t(`lockey_contacts_field_type_${ft}`)}
                    </option>
                  ))}
                </select>
              </div>
            )}
            {formType === 'dropdown' && (
              <div>
                <label className="text-sm font-medium">{t('lockey_contacts_field_options')}</label>
                <textarea
                  value={formOptions}
                  onChange={(e) => setFormOptions(e.target.value)}
                  placeholder={t('lockey_contacts_field_options_placeholder')}
                  className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                  rows={3}
                />
              </div>
            )}
            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                id="isRequired"
                checked={formRequired}
                onChange={(e) => setFormRequired(e.target.checked)}
                className="h-4 w-4 rounded border-input"
              />
              <label htmlFor="isRequired" className="text-sm font-medium">
                {t('lockey_contacts_col_is_required')}
              </label>
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_contacts_col_display_order')}</label>
              <input
                type="number"
                value={formOrder}
                onChange={(e) => setFormOrder(Number(e.target.value))}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                min={0}
              />
            </div>
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
              type="button"
              disabled={!formName.trim() || createField.isPending || updateField.isPending}
              onClick={handleSubmit}
            >
              {editingField
                ? t('lockey_common_save', { ns: 'common' })
                : t('lockey_contacts_create_custom_field')}
            </Button>
          </DialogFooter>
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
