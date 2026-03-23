import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router';
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
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { usePermissions } from '@/shared/hooks/usePermissions';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import {
  useDocument,
  useUpdateDocumentMetadata,
  useArchiveDocument,
  useRestoreDocument,
} from '../hooks/useDocuments';
import { useDocumentVersions, useAddDocumentVersion } from '../hooks/useDocumentVersions';
import { useDocumentAccess, useGrantDocumentAccess, useRevokeDocumentAccess } from '../hooks/useDocumentAccess';
import { DocumentStatusBadge } from '../components/DocumentStatusBadge';
import { FileSize } from '../components/FileSize';
import type { AccessPermission } from '../types';

const ACCESS_PERMISSIONS: AccessPermission[] = ['View', 'Edit', 'Manage'];

function createMetadataSchema(t: (key: string) => string) {
  return z.object({
    name: z.string().min(1, t('lockey_validation_required')),
    description: z.string().optional(),
    tags: z.string().optional(),
  });
}

type MetadataFormValues = z.infer<ReturnType<typeof createMetadataSchema>>;

export default function DocumentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t, i18n } = useTranslation('documents');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const { handleApiError } = useApiError();
  const canUpdate = hasPermission('documents.document.update');
  const canDelete = hasPermission('documents.document.delete');

  const { data: doc, isPending } = useDocument(id ?? '');
  const updateMetadata = useUpdateDocumentMetadata(id ?? '');
  const archiveDoc = useArchiveDocument();
  const restoreDoc = useRestoreDocument();
  const { data: versions } = useDocumentVersions(id ?? '');
  const addVersion = useAddDocumentVersion(id ?? '');
  const { data: accessList } = useDocumentAccess(id ?? '');
  const grantAccess = useGrantDocumentAccess(id ?? '');
  const revokeAccess = useRevokeDocumentAccess(id ?? '');

  const [activeTab, setActiveTab] = useState<'details' | 'versions' | 'access'>('details');
  const [editOpen, setEditOpen] = useState(false);
  const [archiveConfirm, setArchiveConfirm] = useState(false);
  const [restoreConfirm, setRestoreConfirm] = useState(false);
  const [addVersionOpen, setAddVersionOpen] = useState(false);
  const [grantAccessOpen, setGrantAccessOpen] = useState(false);
  const [revokeAccessId, setRevokeAccessId] = useState<string | null>(null);

  const schema = createMetadataSchema(t);
  const metadataForm = useForm<MetadataFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: '', description: '', tags: '' },
  });

  const [versionStorageKey, setVersionStorageKey] = useState('');
  const [versionFileSize, setVersionFileSize] = useState(0);
  const [versionChangeNote, setVersionChangeNote] = useState('');

  const [accessUserId, setAccessUserId] = useState('');
  const [accessRoleId, setAccessRoleId] = useState('');
  const [accessPermission, setAccessPermission] = useState<AccessPermission>('View');

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_documents_module_name' },
      { label: 'lockey_documents_list_title' },
      { label: doc?.name ?? '...' },
    ]);
  }, [setBreadcrumbs, doc?.name]);

  if (isPending) return <LoadingSkeleton />;
  if (!doc) return null;

  const openEdit = () => {
    metadataForm.reset({
      name: doc.name,
      description: doc.description ?? '',
      tags: doc.tags ?? '',
    });
    setEditOpen(true);
  };

  const onEditSubmit = (values: MetadataFormValues) => {
    updateMetadata.mutate(
      { name: values.name, description: values.description, tags: values.tags },
      { onSuccess: () => setEditOpen(false), onError: (err) => handleApiError(err) },
    );
  };

  const tabs = [
    { key: 'details' as const, label: t('lockey_documents_tab_details') },
    { key: 'versions' as const, label: t('lockey_documents_tab_versions') },
    { key: 'access' as const, label: t('lockey_documents_tab_access') },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{doc.name}</h1>
          <div className="mt-1 flex items-center gap-2">
            <DocumentStatusBadge status={doc.status} />
            <span className="text-sm text-muted-foreground">v{doc.currentVersion}</span>
            <span className="text-sm text-muted-foreground">
              <FileSize bytes={doc.fileSize} />
            </span>
          </div>
        </div>
        <div className="flex gap-2">
          {canUpdate && (
            <Button type="button" variant="outline" onClick={openEdit}>
              {t('lockey_documents_action_edit')}
            </Button>
          )}
          {doc.status === 'Active' && canDelete && (
            <Button type="button" variant="outline" onClick={() => setArchiveConfirm(true)}>
              {t('lockey_documents_action_archive')}
            </Button>
          )}
          {doc.status === 'Archived' && (
            <Button type="button" variant="outline" onClick={() => setRestoreConfirm(true)}>
              {t('lockey_documents_action_restore')}
            </Button>
          )}
        </div>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 border-b">
        {tabs.map((tab) => (
          <button
            key={tab.key}
            type="button"
            className={cn(
              'px-4 py-2 text-sm font-medium',
              activeTab === tab.key
                ? 'border-b-2 border-primary text-primary'
                : 'text-muted-foreground hover:text-foreground',
            )}
            onClick={() => setActiveTab(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Details Tab */}
      {activeTab === 'details' && (
        <div className="grid grid-cols-2 gap-4 rounded-lg border p-4">
          <div>
            <p className="text-sm text-muted-foreground">{t('lockey_documents_col_folder')}</p>
            <p className="text-sm font-medium">{doc.folderName}</p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">{t('lockey_documents_col_mime_type')}</p>
            <p className="text-sm font-medium">{doc.mimeType}</p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">{t('lockey_documents_col_created_at')}</p>
            <p className="text-sm font-medium">{new Date(doc.createdAt).toLocaleDateString(i18n.language)}</p>
          </div>
          {doc.updatedAt && (
            <div>
              <p className="text-sm text-muted-foreground">{t('lockey_documents_col_updated_at')}</p>
              <p className="text-sm font-medium">{new Date(doc.updatedAt).toLocaleDateString(i18n.language)}</p>
            </div>
          )}
          {doc.description && (
            <div className="col-span-2">
              <p className="text-sm text-muted-foreground">{t('lockey_documents_form_description')}</p>
              <p className="text-sm">{doc.description}</p>
            </div>
          )}
          {doc.tags && (
            <div className="col-span-2">
              <p className="text-sm text-muted-foreground">{t('lockey_documents_form_tags')}</p>
              <div className="mt-1 flex flex-wrap gap-1">
                {doc.tags.split(',').map((tag) => (
                  <Badge key={tag.trim()} variant="secondary">
                    {tag.trim()}
                  </Badge>
                ))}
              </div>
            </div>
          )}
          {doc.linkedEntityId && (
            <div className="col-span-2">
              <p className="text-sm text-muted-foreground">{t('lockey_documents_link_title')}</p>
              <p className="text-sm font-medium">
                {doc.linkedEntityType}: {doc.linkedEntityId}
              </p>
            </div>
          )}
        </div>
      )}

      {/* Versions Tab */}
      {activeTab === 'versions' && (
        <div className="space-y-4">
          {hasPermission('documents.document.upload') && (
            <Button type="button" onClick={() => setAddVersionOpen(true)}>
              {t('lockey_documents_versions_add')}
            </Button>
          )}
          {versions && versions.length > 0 ? (
            <div className="rounded-lg border">
              <table className="w-full text-sm" aria-label={t('lockey_documents_tab_versions')}>
                <thead>
                  <tr className="border-b bg-muted/50">
                    <th className="px-4 py-2 text-start">{t('lockey_documents_versions_col_number')}</th>
                    <th className="px-4 py-2 text-start">{t('lockey_documents_versions_col_size')}</th>
                    <th className="px-4 py-2 text-start">{t('lockey_documents_versions_col_note')}</th>
                    <th className="px-4 py-2 text-start">{t('lockey_documents_versions_col_date')}</th>
                  </tr>
                </thead>
                <tbody>
                  {versions.map((v) => (
                    <tr key={v.id} className="border-b last:border-0">
                      <td className="px-4 py-2">v{v.versionNumber}</td>
                      <td className="px-4 py-2"><FileSize bytes={v.fileSize} /></td>
                      <td className="px-4 py-2">{v.changeNote ?? '-'}</td>
                      <td className="px-4 py-2">{new Date(v.createdAt).toLocaleDateString(i18n.language)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">{t('lockey_documents_empty_versions')}</p>
          )}
        </div>
      )}

      {/* Access Tab */}
      {activeTab === 'access' && (
        <div className="space-y-4">
          {hasPermission('documents.document.delete') && (
            <Button type="button" onClick={() => setGrantAccessOpen(true)}>
              {t('lockey_documents_access_grant')}
            </Button>
          )}
          {accessList && accessList.length > 0 ? (
            <div className="rounded-lg border">
              <table className="w-full text-sm" aria-label={t('lockey_documents_tab_access')}>
                <thead>
                  <tr className="border-b bg-muted/50">
                    <th className="px-4 py-2 text-start">{t('lockey_documents_access_col_user')}</th>
                    <th className="px-4 py-2 text-start">{t('lockey_documents_access_col_role')}</th>
                    <th className="px-4 py-2 text-start">{t('lockey_documents_access_col_permission')}</th>
                    <th className="px-4 py-2 text-start">{t('lockey_documents_col_actions')}</th>
                  </tr>
                </thead>
                <tbody>
                  {accessList.map((a) => (
                    <tr key={a.id} className="border-b last:border-0">
                      <td className="px-4 py-2">{a.userId ?? '-'}</td>
                      <td className="px-4 py-2">{a.roleId ?? '-'}</td>
                      <td className="px-4 py-2">
                        <Badge variant="outline">
                          {t(`lockey_documents_access_permission_${a.permission.toLowerCase()}`)}
                        </Badge>
                      </td>
                      <td className="px-4 py-2">
                        {hasPermission('documents.document.delete') && (
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            onClick={() => setRevokeAccessId(a.id)}
                          >
                            {t('lockey_documents_access_revoke')}
                          </Button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">{t('lockey_documents_empty_access')}</p>
          )}
        </div>
      )}

      {/* Edit Metadata Dialog */}
      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('lockey_documents_action_edit')}</DialogTitle>
          </DialogHeader>
          <form onSubmit={metadataForm.handleSubmit(onEditSubmit)} className="space-y-4">
            <div>
              <label className="text-sm font-medium">{t('lockey_documents_form_name')}</label>
              <input
                type="text"
                {...metadataForm.register('name')}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
              {metadataForm.formState.errors.name?.message && (
                <p className="mt-1 text-sm text-destructive">
                  {metadataForm.formState.errors.name.message}
                </p>
              )}
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_documents_form_description')}</label>
              <textarea
                {...metadataForm.register('description')}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                rows={3}
              />
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_documents_form_tags')}</label>
              <input
                type="text"
                {...metadataForm.register('tags')}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setEditOpen(false)}>
                {t('lockey_common_cancel', { ns: 'common' })}
              </Button>
              <Button type="submit" disabled={updateMetadata.isPending}>
                {t('lockey_common_save', { ns: 'common' })}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Add Version Dialog */}
      <Dialog open={addVersionOpen} onOpenChange={setAddVersionOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('lockey_documents_versions_add')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <label className="text-sm font-medium">{t('lockey_documents_templates_form_storage_key')}</label>
              <input
                type="text"
                value={versionStorageKey}
                onChange={(e) => setVersionStorageKey(e.target.value)}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_documents_form_file_size')}</label>
              <input
                type="number"
                value={versionFileSize}
                onChange={(e) => setVersionFileSize(Number(e.target.value))}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                min={0}
              />
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_documents_versions_form_change_note')}</label>
              <input
                type="text"
                value={versionChangeNote}
                onChange={(e) => setVersionChangeNote(e.target.value)}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
            </div>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setAddVersionOpen(false)}>
              {t('lockey_common_cancel', { ns: 'common' })}
            </Button>
            <Button
              type="button"
              disabled={addVersion.isPending || !versionStorageKey}
              onClick={() => {
                addVersion.mutate(
                  {
                    storageKey: versionStorageKey,
                    fileSize: versionFileSize,
                    changeNote: versionChangeNote || undefined,
                  },
                  {
                    onSuccess: () => {
                      setAddVersionOpen(false);
                      setVersionStorageKey('');
                      setVersionFileSize(0);
                      setVersionChangeNote('');
                    },
                    onError: (err) => handleApiError(err),
                  },
                );
              }}
            >
              {t('lockey_documents_versions_add')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Grant Access Dialog */}
      <Dialog open={grantAccessOpen} onOpenChange={setGrantAccessOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('lockey_documents_access_grant')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <label className="text-sm font-medium">{t('lockey_documents_access_form_user_id')}</label>
              <input
                type="text"
                value={accessUserId}
                onChange={(e) => setAccessUserId(e.target.value)}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_documents_access_form_role_id')}</label>
              <input
                type="text"
                value={accessRoleId}
                onChange={(e) => setAccessRoleId(e.target.value)}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_documents_access_form_permission')}</label>
              <Select
                value={accessPermission}
                onValueChange={(v) => setAccessPermission(v as AccessPermission)}
              >
                <SelectTrigger className="mt-1" aria-label={t('lockey_documents_access_form_permission')}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {ACCESS_PERMISSIONS.map((p) => (
                    <SelectItem key={p} value={p}>
                      {t(`lockey_documents_access_permission_${p.toLowerCase()}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setGrantAccessOpen(false)}>
              {t('lockey_common_cancel', { ns: 'common' })}
            </Button>
            <Button
              type="button"
              disabled={grantAccess.isPending || (!accessUserId && !accessRoleId)}
              onClick={() => {
                grantAccess.mutate(
                  {
                    userId: accessUserId || undefined,
                    roleId: accessRoleId || undefined,
                    permission: accessPermission,
                  },
                  {
                    onSuccess: () => {
                      setGrantAccessOpen(false);
                      setAccessUserId('');
                      setAccessRoleId('');
                      setAccessPermission('View');
                    },
                    onError: (err) => handleApiError(err),
                  },
                );
              }}
            >
              {t('lockey_documents_access_grant')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Archive Confirm */}
      <ConfirmDialog
        open={archiveConfirm}
        onOpenChange={setArchiveConfirm}
        title={t('lockey_documents_confirm_archive_title')}
        description={t('lockey_documents_confirm_archive')}
        onConfirm={() => {
          archiveDoc.mutate(id!, {
            onSuccess: () => {
              setArchiveConfirm(false);
              navigate('/documents/documents');
            },
            onError: (err) => handleApiError(err),
          });
        }}
        isPending={archiveDoc.isPending}
      />

      {/* Restore Confirm */}
      <ConfirmDialog
        open={restoreConfirm}
        onOpenChange={setRestoreConfirm}
        title={t('lockey_documents_confirm_restore_title')}
        description={t('lockey_documents_confirm_restore')}
        onConfirm={() => {
          restoreDoc.mutate(id!, {
            onSuccess: () => setRestoreConfirm(false),
            onError: (err) => handleApiError(err),
          });
        }}
        isPending={restoreDoc.isPending}
      />

      {/* Revoke Access Confirm */}
      <ConfirmDialog
        open={revokeAccessId !== null}
        onOpenChange={() => setRevokeAccessId(null)}
        title={t('lockey_documents_access_confirm_revoke_title')}
        description={t('lockey_documents_access_confirm_revoke')}
        variant="destructive"
        onConfirm={() => {
          if (revokeAccessId) {
            revokeAccess.mutate(revokeAccessId, {
              onSuccess: () => setRevokeAccessId(null),
              onError: (err) => {
                handleApiError(err);
                setRevokeAccessId(null);
              },
            });
          }
        }}
        isPending={revokeAccess.isPending}
      />
    </div>
  );
}
