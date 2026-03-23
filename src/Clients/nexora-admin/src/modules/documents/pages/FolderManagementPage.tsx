import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { cn } from '@/shared/lib/utils';
import { useApiError } from '@/shared/hooks/useApiError';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { useFolders, useCreateFolder, useRenameFolder, useDeleteFolder } from '../hooks/useFolders';
import type { FolderDto } from '../types';

function createFolderSchema(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    name: z.string().min(1, t('lockey_validation_required', { ns: 'validation' })),
  });
}

type FolderFormValues = z.infer<ReturnType<typeof createFolderSchema>>;

export default function FolderManagementPage() {
  const { t, i18n } = useTranslation('documents');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const { handleApiError } = useApiError();
  const canManage = hasPermission('documents.folder.manage');

  const [currentParentId, setCurrentParentId] = useState<string | undefined>(undefined);
  const [breadcrumbPath, setBreadcrumbPath] = useState<{ id: string; name: string }[]>([]);
  const { data: folders, isPending } = useFolders(currentParentId);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingFolder, setEditingFolder] = useState<FolderDto | null>(null);
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);

  const createFolder = useCreateFolder();
  const renameFolder = useRenameFolder(editingFolder?.id ?? '');
  const deleteFolder = useDeleteFolder();

  const schema = createFolderSchema(t);
  const form = useForm<FolderFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: '' },
  });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_documents_module_name' },
      { label: 'lockey_documents_folders_title' },
    ]);
  }, [setBreadcrumbs]);

  const navigateToFolder = (folder: FolderDto) => {
    setCurrentParentId(folder.id);
    setBreadcrumbPath((prev) => [...prev, { id: folder.id, name: folder.name }]);
  };

  const navigateUp = (index: number) => {
    if (index < 0) {
      setCurrentParentId(undefined);
      setBreadcrumbPath([]);
    } else {
      setCurrentParentId(breadcrumbPath[index]?.id);
      setBreadcrumbPath((prev) => prev.slice(0, index + 1));
    }
  };

  const openCreateDialog = () => {
    setEditingFolder(null);
    form.reset({ name: '' });
    setDialogOpen(true);
  };

  const openRenameDialog = (folder: FolderDto) => {
    setEditingFolder(folder);
    form.reset({ name: folder.name });
    setDialogOpen(true);
  };

  const onSubmit = (values: FolderFormValues) => {
    if (editingFolder) {
      renameFolder.mutate(
        { newName: values.name },
        { onSuccess: () => { form.reset(); setDialogOpen(false); }, onError: (err) => handleApiError(err) },
      );
    } else {
      createFolder.mutate(
        { name: values.name, parentFolderId: currentParentId },
        { onSuccess: () => { form.reset(); setDialogOpen(false); }, onError: (err) => handleApiError(err) },
      );
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('lockey_documents_folders_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_documents_folders_description')}
          </p>
        </div>
        {canManage && (
          <Button type="button" onClick={openCreateDialog}>
            {t('lockey_documents_folders_create')}
          </Button>
        )}
      </div>

      {/* Breadcrumb navigation */}
      <div className="flex items-center gap-1 text-sm">
        <button
          type="button"
          className={cn('hover:underline', !currentParentId && 'font-semibold')}
          onClick={() => navigateUp(-1)}
        >
          {t('lockey_documents_folders_root')}
        </button>
        {breadcrumbPath.map((item, idx) => (
          <span key={item.id} className="flex items-center gap-1">
            <span>/</span>
            <button
              type="button"
              className={cn('hover:underline', idx === breadcrumbPath.length - 1 && 'font-semibold')}
              onClick={() => navigateUp(idx)}
            >
              {item.name}
            </button>
          </span>
        ))}
      </div>

      {/* Folder list */}
      {isPending ? (
        <div className="text-sm text-muted-foreground">{t('lockey_common_loading', { ns: 'common' })}</div>
      ) : folders && folders.length > 0 ? (
        <div className="rounded-lg border">
          <table className="w-full text-sm" aria-label={t('lockey_documents_folders_title')}>
            <thead>
              <tr className="border-b bg-muted/50">
                <th className="px-4 py-2 text-start">{t('lockey_documents_folders_col_name')}</th>
                <th className="px-4 py-2 text-start">{t('lockey_documents_folders_col_path')}</th>
                <th className="px-4 py-2 text-start">{t('lockey_documents_folders_col_system')}</th>
                <th className="px-4 py-2 text-start">{t('lockey_documents_folders_col_created_at')}</th>
                <th className="px-4 py-2 text-start">{t('lockey_documents_col_actions')}</th>
              </tr>
            </thead>
            <tbody>
              {folders.map((folder) => (
                <tr key={folder.id} className="border-b last:border-0">
                  <td className="px-4 py-2">
                    <button
                      type="button"
                      className="font-medium hover:underline"
                      onClick={() => navigateToFolder(folder)}
                    >
                      {folder.name}
                    </button>
                  </td>
                  <td className="px-4 py-2 text-muted-foreground">{folder.path}</td>
                  <td className="px-4 py-2">
                    {folder.isSystem && (
                      <Badge variant="secondary">{t('lockey_documents_folders_col_system')}</Badge>
                    )}
                  </td>
                  <td className="px-4 py-2">
                    {new Date(folder.createdAt).toLocaleDateString(i18n.language)}
                  </td>
                  <td className="px-4 py-2">
                    {canManage && !folder.isSystem && (
                      <div className="flex gap-1">
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={() => openRenameDialog(folder)}
                        >
                          {t('lockey_documents_folders_rename')}
                        </Button>
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={() => setDeleteConfirmId(folder.id)}
                        >
                          {t('lockey_documents_folders_delete')}
                        </Button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">{t('lockey_documents_folders_empty')}</p>
      )}

      {/* Create/Rename Dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {editingFolder
                ? t('lockey_documents_folders_rename')
                : t('lockey_documents_folders_create')}
            </DialogTitle>
            <DialogDescription className="sr-only">
              {editingFolder
                ? t('lockey_documents_folders_rename')
                : t('lockey_documents_folders_create')}
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <div>
              <label htmlFor="folder-name" className="text-sm font-medium">{t('lockey_documents_folders_form_name')}</label>
              <input
                id="folder-name"
                type="text"
                {...form.register('name')}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
              {form.formState.errors.name?.message && (
                <p className="mt-1 text-sm text-destructive">
                  {form.formState.errors.name.message}
                </p>
              )}
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setDialogOpen(false)}>
                {t('lockey_common_cancel', { ns: 'common' })}
              </Button>
              <Button
                type="submit"
                disabled={createFolder.isPending || renameFolder.isPending}
              >
                {editingFolder
                  ? t('lockey_common_save', { ns: 'common' })
                  : t('lockey_documents_folders_create')}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Delete Confirm */}
      <ConfirmDialog
        open={deleteConfirmId !== null}
        onOpenChange={() => setDeleteConfirmId(null)}
        title={t('lockey_documents_folders_confirm_delete_title')}
        description={t('lockey_documents_folders_confirm_delete')}
        variant="destructive"
        onConfirm={() => {
          if (deleteConfirmId) {
            deleteFolder.mutate(deleteConfirmId, {
              onSuccess: () => setDeleteConfirmId(null),
              onError: (err) => {
                handleApiError(err);
                setDeleteConfirmId(null);
              },
            });
          }
        }}
        isPending={deleteFolder.isPending}
      />
    </div>
  );
}
