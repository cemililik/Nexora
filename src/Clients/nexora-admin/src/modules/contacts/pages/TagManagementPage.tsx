import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

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
import { useTags, useCreateTag, useUpdateTag, useDeleteTag } from '../hooks/useTags';
import type { TagDto, TagCategory, CreateTagRequest, UpdateTagRequest } from '../types';

const TAG_CATEGORIES: TagCategory[] = [
  'Donor', 'Parent', 'Volunteer', 'Vendor', 'Student', 'Staff',
];

export default function TagManagementPage() {
  const { t, i18n } = useTranslation('contacts');
  const { page, pageSize, setPage } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { handleApiError } = useApiError();

  const { data: tags, isPending } = useTags();
  const createTag = useCreateTag();
  const [editingTag, setEditingTag] = useState<TagDto | null>(null);
  const updateTag = useUpdateTag(editingTag?.id ?? '');
  const deleteTag = useDeleteTag();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);

  // Form state
  const [formName, setFormName] = useState('');
  const [formCategory, setFormCategory] = useState<TagCategory>('Donor');
  const [formColor, setFormColor] = useState('');

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_contacts_module_name' },
      { label: 'lockey_contacts_tags_title' },
    ]);
  }, [setBreadcrumbs]);

  const openCreateDialog = () => {
    setEditingTag(null);
    setFormName('');
    setFormCategory('Donor');
    setFormColor('');
    setDialogOpen(true);
  };

  const openEditDialog = (tag: TagDto) => {
    setEditingTag(tag);
    setFormName(tag.name);
    setFormCategory(tag.category);
    setFormColor(tag.color ?? '');
    setDialogOpen(true);
  };

  const handleSubmit = () => {
    if (editingTag) {
      const data: UpdateTagRequest = {
        name: formName,
        category: formCategory,
        color: formColor || undefined,
      };
      updateTag.mutate(data, {
        onSuccess: () => setDialogOpen(false),
        onError: (err) => handleApiError(err),
      });
    } else {
      const data: CreateTagRequest = {
        name: formName,
        category: formCategory,
        color: formColor || undefined,
      };
      createTag.mutate(data, {
        onSuccess: () => setDialogOpen(false),
        onError: (err) => handleApiError(err),
      });
    }
  };

  // Client-side pagination since useTags returns full list
  const allTags = tags ?? [];
  const totalCount = allTags.length;
  const paginatedTags = allTags.slice((page - 1) * pageSize, page * pageSize);

  const columns: ColumnDef<TagDto>[] = [
    {
      key: 'name',
      header: t('lockey_contacts_tags_col_name'),
      render: (row) => row.name,
    },
    {
      key: 'category',
      header: t('lockey_contacts_tags_col_category'),
      render: (row) => t(`lockey_contacts_tag_category_${row.category.toLowerCase()}`),
    },
    {
      key: 'color',
      header: t('lockey_contacts_tags_col_color'),
      render: (row) =>
        row.color ? (
          <span
            className="inline-block h-4 w-4 rounded-full border"
            // Inline style required: dynamic tag color from data
            style={{ backgroundColor: row.color }}
          />
        ) : (
          '—'
        ),
    },
    {
      key: 'isActive',
      header: t('lockey_contacts_tags_col_active'),
      render: (row) => (
        <Badge variant={row.isActive ? 'default' : 'secondary'}>
          {row.isActive
            ? t('lockey_contacts_active')
            : t('lockey_contacts_inactive')}
        </Badge>
      ),
    },
    {
      key: 'createdAt',
      header: t('lockey_contacts_col_created_at'),
      render: (row) => new Date(row.createdAt).toLocaleDateString(i18n.language),
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
          <h1 className="text-2xl font-semibold">{t('lockey_contacts_tags_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_contacts_tags_description')}
          </p>
        </div>
        <Button type="button" onClick={openCreateDialog}>
          {t('lockey_contacts_tags_create')}
        </Button>
      </div>

      <DataTable
        columns={columns}
        data={paginatedTags}
        totalCount={totalCount}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        isLoading={isPending}
        emptyMessage={t('lockey_contacts_empty_tags')}
        keyExtractor={(row) => row.id}
      />

      {/* Create/Edit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {editingTag
                ? t('lockey_contacts_tags_edit')
                : t('lockey_contacts_tags_create')}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <label className="text-sm font-medium">{t('lockey_contacts_tags_col_name')}</label>
              <input
                type="text"
                value={formName}
                onChange={(e) => setFormName(e.target.value)}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_contacts_tags_col_category')}</label>
              <select
                value={formCategory}
                onChange={(e) => setFormCategory(e.target.value as TagCategory)}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              >
                {TAG_CATEGORIES.map((cat) => (
                  <option key={cat} value={cat}>
                    {t(`lockey_contacts_tag_category_${cat.toLowerCase()}`)}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_contacts_tags_col_color')}</label>
              <input
                type="text"
                value={formColor}
                onChange={(e) => setFormColor(e.target.value)}
                placeholder="#FF5733"
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
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
              disabled={!formName.trim() || createTag.isPending || updateTag.isPending}
              onClick={handleSubmit}
            >
              {editingTag
                ? t('lockey_common_save', { ns: 'common' })
                : t('lockey_contacts_tags_create')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete Confirm */}
      <ConfirmDialog
        open={deleteConfirmId !== null}
        onOpenChange={() => setDeleteConfirmId(null)}
        title={t('lockey_contacts_delete_tag_title')}
        description={t('lockey_contacts_delete_tag_description')}
        variant="destructive"
        onConfirm={() => {
          if (deleteConfirmId) {
            deleteTag.mutate(deleteConfirmId, {
              onError: (err) => handleApiError(err),
            });
          }
          setDeleteConfirmId(null);
        }}
        isPending={deleteTag.isPending}
      />
    </div>
  );
}
