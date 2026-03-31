import { useEffect, useMemo, useState, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { cn } from '@/shared/lib/utils';
import { FileDropZone } from '../components/FileDropZone';
import { useFileUpload } from '../hooks/useFileUpload';
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
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { TabContentSkeleton } from '@/shared/components/feedback/TabContentSkeleton';
import { SearchableDropdown } from '@/shared/components/data/SearchableDropdown';
import { useUnsavedChangesGuard } from '@/shared/hooks/useUnsavedChangesGuard';
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
  useDocumentDownloadUrl,
  useUpdateDocumentMetadata,
  useArchiveDocument,
  useRestoreDocument,
} from '../hooks/useDocuments';
import { useDocumentVersions } from '../hooks/useDocumentVersions';
import { useDocumentAccess, useGrantDocumentAccess, useRevokeDocumentAccess } from '../hooks/useDocumentAccess';
import { useSignatures } from '../hooks/useSignatures';
import { useUsers } from '@/modules/identity/hooks/useUsers';
import { useRoles } from '@/modules/identity/hooks/useRoles';
import { DocumentStatusBadge } from '../components/DocumentStatusBadge';
import { FileSize } from '../components/FileSize';
import type { AccessPermission } from '../types';

const ACCESS_PERMISSIONS: AccessPermission[] = ['View', 'Edit', 'Manage'];

function isPreviewable(mimeType: string): boolean {
  if (mimeType === 'application/pdf') return true;
  if (mimeType.startsWith('image/')) return true;
  return false;
}

function createVersionSchema() {
  return z.object({
    changeNote: z.string().optional(),
  });
}

type VersionFormValues = z.infer<ReturnType<typeof createVersionSchema>>;

function createAccessSchema(t: (key: string, options?: Record<string, string>) => string) {
  return z.object({
    userId: z.string().optional(),
    roleId: z.string().optional(),
    permission: z.enum(['View', 'Edit', 'Manage']),
  }).refine(
    (data) => (data.userId && data.userId.length > 0) || (data.roleId && data.roleId.length > 0),
    { message: t('lockey_validation_required', { ns: 'validation' }), path: ['userId'] },
  );
}

type AccessFormValues = z.infer<ReturnType<typeof createAccessSchema>>;

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
  const { data: versions, isPending: isVersionsPending } = useDocumentVersions(id ?? '');
  // addVersion replaced by useFileUpload (FileDropZone-based upload in version dialog)
  const { data: accessList, isPending: isAccessPending } = useDocumentAccess(id ?? '');
  const grantAccess = useGrantDocumentAccess(id ?? '');
  const revokeAccess = useRevokeDocumentAccess(id ?? '');
  const { refetch: fetchDownloadUrl } = useDocumentDownloadUrl(id ?? '');

  // Signatures for this document
  const { data: signaturesData, isPending: isSignaturesPending } = useSignatures({
    page: 1,
    pageSize: 50,
    documentId: id,
  });

  // User / role resolution for access tab
  const [userSearch, setUserSearch] = useState('');
  const [debouncedUserSearch, setDebouncedUserSearch] = useState('');
  const [selectedUser, setSelectedUser] = useState<{ id: string; firstName: string; lastName: string; email: string } | null>(null);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedUserSearch(userSearch), 300);
    return () => clearTimeout(timer);
  }, [userSearch]);

  const { data: usersData, isPending: isUserSearchPending } = useUsers({
    page: 1,
    pageSize: 100,
    search: debouncedUserSearch || undefined,
  });
  // Separate user lookup (no search filter) for resolving names in the access list
  const { data: lookupUsersData } = useUsers({
    page: 1,
    pageSize: 100,
  });
  const { data: rolesData } = useRoles();

  const resolveUserName = useCallback(
    (userId: string): string => {
      const user = lookupUsersData?.items?.find((u) => u.id === userId);
      return user ? `${user.firstName} ${user.lastName}` : userId;
    },
    [lookupUsersData],
  );

  const resolveUserEmail = useCallback(
    (userId: string): string => {
      const user = lookupUsersData?.items?.find((u) => u.id === userId);
      return user?.email ?? '';
    },
    [lookupUsersData],
  );

  const resolveRoleName = useCallback(
    (roleId: string): string => {
      const role = rolesData?.find((r) => r.id === roleId);
      return role?.name ?? roleId;
    },
    [rolesData],
  );

  const [activeTab, setActiveTab] = useState<'details' | 'versions' | 'signatures' | 'access'>('details');
  const [editOpen, setEditOpen] = useState(false);
  const [archiveConfirm, setArchiveConfirm] = useState(false);
  const [restoreConfirm, setRestoreConfirm] = useState(false);
  const [addVersionOpen, setAddVersionOpen] = useState(false);
  const [versionFile, setVersionFile] = useState<File | null>(null);
  const versionUpload = useFileUpload();
  const [grantAccessOpen, setGrantAccessOpen] = useState(false);
  const [revokeAccessId, setRevokeAccessId] = useState<string | null>(null);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);

  const metadataSchema = useMemo(() => createMetadataSchema(t), [t]);
  const metadataForm = useForm<MetadataFormValues>({
    resolver: zodResolver(metadataSchema),
    defaultValues: { name: '', description: '', tags: '' },
  });

  const { isBlocked: isMetadataBlocked, proceed: proceedMetadata, reset: resetMetadata } =
    useUnsavedChangesGuard(editOpen && metadataForm.formState.isDirty);

  const versionSchema = useMemo(() => createVersionSchema(), []);
  const versionForm = useForm<VersionFormValues>({
    resolver: zodResolver(versionSchema),
    defaultValues: { changeNote: '' },
  });

  const accessSchema = useMemo(() => createAccessSchema(t), [t]);
  const accessForm = useForm<AccessFormValues>({
    resolver: zodResolver(accessSchema),
    defaultValues: { userId: '', roleId: '', permission: 'View' },
  });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_documents_module_name' },
      { label: 'lockey_documents_list_title', path: '/documents' },
      { label: doc?.name ?? '...' },
    ]);
  }, [setBreadcrumbs, doc?.name]);

  if (isPending) return <LoadingSkeleton />;
  if (!doc) return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">{t('lockey_documents_empty_documents')}</p>
      <Button type="button" variant="outline" onClick={() => navigate(-1)}>
        {t('lockey_common_back', { ns: 'common' })}
      </Button>
    </div>
  );

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

  const handlePreview = async () => {
    const result = await fetchDownloadUrl();
    if (result.data) {
      setPreviewUrl(result.data.downloadUrl);
      setPreviewOpen(true);
    }
  };

  const handleOpenInNewTab = async () => {
    const result = await fetchDownloadUrl();
    if (result.data) {
      window.open(result.data.downloadUrl, '_blank', 'noopener,noreferrer');
    }
  };

  const tabs = [
    { key: 'details' as const, label: t('lockey_documents_tab_details') },
    { key: 'versions' as const, label: t('lockey_documents_tab_versions') },
    { key: 'signatures' as const, label: t('lockey_documents_tab_signatures') },
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
          {doc.status === 'Archived' && canDelete && (
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
            onClick={() => { setActiveTab(tab.key); window.scrollTo({ top: 0, behavior: 'smooth' }); }}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Details Tab */}
      {activeTab === 'details' && (
        <div className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 rounded-lg border p-4">
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

          {/* Preview / Open buttons */}
          <div className="flex gap-2">
            {isPreviewable(doc.mimeType) && (
              <Button type="button" variant="outline" onClick={handlePreview}>
                {t('lockey_documents_action_preview')}
              </Button>
            )}
            <Button type="button" variant="outline" onClick={handleOpenInNewTab}>
              {t('lockey_documents_action_open_new_tab')}
            </Button>
          </div>
        </div>
      )}

      {/* Versions Tab */}
      {activeTab === 'versions' && (
        isVersionsPending ? <TabContentSkeleton variant="list" /> : (
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
        )
      )}

      {/* Signatures Tab */}
      {activeTab === 'signatures' && (
        isSignaturesPending ? <TabContentSkeleton variant="list" /> : (
        <div className="space-y-4">
          {signaturesData?.items && signaturesData.items.length > 0 ? (
            <div className="rounded-lg border">
              <table className="w-full text-sm" aria-label={t('lockey_documents_tab_signatures')}>
                <thead>
                  <tr className="border-b bg-muted/50">
                    <th className="px-4 py-2 text-start">{t('lockey_documents_signatures_col_title')}</th>
                    <th className="px-4 py-2 text-start">{t('lockey_documents_signatures_col_status')}</th>
                    <th className="px-4 py-2 text-start">{t('lockey_documents_signatures_col_recipients')}</th>
                    <th className="px-4 py-2 text-start">{t('lockey_documents_signatures_col_created_at')}</th>
                    <th className="px-4 py-2 text-start">{t('lockey_documents_signatures_col_actions')}</th>
                  </tr>
                </thead>
                <tbody>
                  {signaturesData.items.map((sig) => (
                    <tr key={sig.id} className="border-b last:border-0">
                      <td className="px-4 py-2">{sig.title}</td>
                      <td className="px-4 py-2">
                        <Badge variant="outline">
                          {t(`lockey_documents_signatures_status_${sig.status.toLowerCase()}`)}
                        </Badge>
                      </td>
                      <td className="px-4 py-2">{sig.recipientCount}</td>
                      <td className="px-4 py-2">{new Date(sig.createdAt).toLocaleDateString(i18n.language)}</td>
                      <td className="px-4 py-2">
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={() => navigate(`/documents/signatures/${sig.id}`)}
                        >
                          {t('lockey_documents_action_edit')}
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">{t('lockey_documents_signatures_empty')}</p>
          )}
        </div>
        )
      )}

      {/* Access Tab */}
      {activeTab === 'access' && (
        isAccessPending ? <TabContentSkeleton variant="list" /> : (
        <div className="space-y-4">
          {/* Default access info banner */}
          <div className="rounded-lg border border-blue-200 bg-blue-50 p-3 dark:border-blue-800 dark:bg-blue-950">
            <p className="text-sm text-blue-800 dark:text-blue-200">
              {t('lockey_documents_access_default_info')}
            </p>
          </div>

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
                      <td className="px-4 py-2">
                        {a.userId ? (
                          <div>
                            <p className="font-medium">{resolveUserName(a.userId)}</p>
                            <p className="text-xs text-muted-foreground">{resolveUserEmail(a.userId)}</p>
                          </div>
                        ) : (
                          '-'
                        )}
                      </td>
                      <td className="px-4 py-2">
                        {a.roleId ? (
                          <Badge variant="secondary">{resolveRoleName(a.roleId)}</Badge>
                        ) : (
                          '-'
                        )}
                      </td>
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
        )
      )}

      {/* Preview Dialog */}
      <Dialog open={previewOpen} onOpenChange={setPreviewOpen}>
        <DialogContent className="max-w-4xl">
          <DialogHeader>
            <DialogTitle>{t('lockey_documents_action_preview')}</DialogTitle>
            <DialogDescription className="sr-only">{t('lockey_documents_action_preview')}</DialogDescription>
          </DialogHeader>
          <div className="min-h-[60vh]">
            {previewUrl && doc.mimeType === 'application/pdf' && (
              <iframe
                src={previewUrl}
                title={doc.name}
                className="h-[60vh] w-full rounded border"
              />
            )}
            {previewUrl && doc.mimeType.startsWith('image/') && (
              <img
                src={previewUrl}
                alt={doc.name}
                className="mx-auto max-h-[60vh] rounded object-contain"
              />
            )}
            {previewUrl && !isPreviewable(doc.mimeType) && (
              <div className="flex flex-col items-center justify-center gap-4 py-16">
                <p className="text-sm text-muted-foreground">
                  {t('lockey_documents_preview_not_available')}
                </p>
                <Button type="button" variant="outline" onClick={handleOpenInNewTab}>
                  {t('lockey_documents_action_open_new_tab')}
                </Button>
              </div>
            )}
          </div>
        </DialogContent>
      </Dialog>

      {/* Edit Metadata Dialog */}
      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('lockey_documents_action_edit')}</DialogTitle>
            <DialogDescription className="sr-only">{t('lockey_documents_action_edit')}</DialogDescription>
          </DialogHeader>
          <form onSubmit={metadataForm.handleSubmit(onEditSubmit)} className="space-y-4">
            <div>
              <label htmlFor="metadata-name" className="text-sm font-medium">{t('lockey_documents_form_name')}</label>
              <input
                id="metadata-name"
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
              <label htmlFor="metadata-description" className="text-sm font-medium">{t('lockey_documents_form_description')}</label>
              <textarea
                id="metadata-description"
                {...metadataForm.register('description')}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                rows={3}
              />
            </div>
            <div>
              <label htmlFor="metadata-tags" className="text-sm font-medium">{t('lockey_documents_form_tags')}</label>
              <input
                id="metadata-tags"
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

      {/* Add Version Dialog — uses FileDropZone for file upload */}
      <Dialog open={addVersionOpen} onOpenChange={(open) => {
        if (!open) { versionUpload.reset(); setVersionFile(null); versionForm.reset(); }
        setAddVersionOpen(open);
      }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('lockey_documents_versions_add')}</DialogTitle>
            <DialogDescription className="sr-only">{t('lockey_documents_versions_add')}</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <FileDropZone
              onFileSelect={setVersionFile}
              disabled={versionUpload.state !== 'idle'}
              isUploading={versionUpload.state === 'uploading'}
              progress={versionUpload.progress}
            />
            <div>
              <label htmlFor="version-change-note" className="text-sm font-medium">
                {t('lockey_documents_versions_form_change_note')}
              </label>
              <input
                id="version-change-note"
                type="text"
                {...versionForm.register('changeNote')}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                disabled={versionUpload.state !== 'idle'}
              />
            </div>
            {versionUpload.state === 'uploading' && (
              <div className="space-y-1">
                <div className="h-2 w-full rounded-full bg-secondary">
                  <div className="h-2 rounded-full bg-primary transition-all" style={{ width: `${versionUpload.progress}%` }} />
                </div>
                <p className="text-xs text-muted-foreground text-center">{versionUpload.progress}%</p>
              </div>
            )}
            {versionUpload.error && (
              <p className="text-sm text-destructive">{versionUpload.error}</p>
            )}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setAddVersionOpen(false)}>
                {t('lockey_common_cancel', { ns: 'common' })}
              </Button>
              <Button
                type="button"
                disabled={!versionFile || versionUpload.state !== 'idle'}
                onClick={async () => {
                  if (!versionFile || !doc) return;
                  try {
                    await versionUpload.upload(versionFile, {
                      folderId: doc.folderId,
                      name: doc.name,
                      description: versionForm.getValues('changeNote') || undefined,
                    });
                    setAddVersionOpen(false);
                    setVersionFile(null);
                    versionForm.reset();
                    versionUpload.reset();
                  } catch {
                    // Keep dialog open and state intact on error
                  }
                }}
              >
                {t('lockey_documents_versions_add')}
              </Button>
            </DialogFooter>
          </div>
        </DialogContent>
      </Dialog>

      {/* Grant Access Dialog */}
      <Dialog open={grantAccessOpen} onOpenChange={setGrantAccessOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('lockey_documents_access_grant')}</DialogTitle>
            <DialogDescription className="sr-only">{t('lockey_documents_access_grant')}</DialogDescription>
          </DialogHeader>
          <form
            onSubmit={accessForm.handleSubmit((values) => {
              grantAccess.mutate(
                {
                  userId: values.userId || undefined,
                  roleId: values.roleId || undefined,
                  permission: values.permission,
                },
                {
                  onSuccess: () => {
                    setGrantAccessOpen(false);
                    accessForm.reset();
                    setUserSearch('');
                    setSelectedUser(null);
                  },
                  onError: (err) => handleApiError(err),
                },
              );
            })}
            className="space-y-4"
          >
            <div>
              <SearchableDropdown
                value={selectedUser}
                onSelect={(u) => {
                  setSelectedUser(u);
                  accessForm.setValue('userId', u.id);
                  setUserSearch(`${u.firstName} ${u.lastName} (${u.email})`);
                }}
                items={usersData?.items ?? []}
                isLoading={isUserSearchPending && debouncedUserSearch.length > 0}
                searchValue={userSearch}
                onSearchChange={setUserSearch}
                keyExtractor={(u) => u.id}
                renderItem={(u) => (
                  <>
                    <span className="font-medium">{u.firstName} {u.lastName}</span>
                    <span className="text-xs text-muted-foreground">{u.email}</span>
                  </>
                )}
                renderSelected={(u) => `${u.firstName} ${u.lastName} (${u.email})`}
                label={t('lockey_documents_access_form_user_search')}
                placeholder={t('lockey_documents_access_form_user_search_placeholder')}
              />
              {accessForm.formState.errors.userId?.message && (
                <p className="mt-1 text-sm text-destructive">
                  {accessForm.formState.errors.userId.message}
                </p>
              )}
            </div>
            <div>
              <label htmlFor="access-role-id" className="text-sm font-medium">{t('lockey_documents_access_form_role_id')}</label>
              <Controller
                control={accessForm.control}
                name="roleId"
                render={({ field }) => (
                  <Select value={field.value ?? '__none__'} onValueChange={(v) => field.onChange(v === '__none__' ? '' : v)}>
                    <SelectTrigger className="mt-1" aria-label={t('lockey_documents_access_form_role_id')}>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__none__">-</SelectItem>
                      {rolesData?.map((r) => (
                        <SelectItem key={r.id} value={r.id}>
                          {r.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
            <div>
              <label className="text-sm font-medium">{t('lockey_documents_access_form_permission')}</label>
              <Controller
                control={accessForm.control}
                name="permission"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
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
                )}
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setGrantAccessOpen(false)}>
                {t('lockey_common_cancel', { ns: 'common' })}
              </Button>
              <Button type="submit" disabled={grantAccess.isPending}>
                {t('lockey_documents_access_grant')}
              </Button>
            </DialogFooter>
          </form>
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

      {/* Unsaved Changes Guard */}
      <ConfirmDialog
        open={isMetadataBlocked}
        onOpenChange={(open) => { if (!open) resetMetadata(); }}
        title={t('lockey_common_unsaved_changes_title', { ns: 'common' })}
        description={t('lockey_common_unsaved_changes_description', { ns: 'common' })}
        onConfirm={proceedMetadata}
        confirmLabel={t('lockey_common_leave', { ns: 'common' })}
        cancelLabel={t('lockey_common_stay', { ns: 'common' })}
        variant="destructive"
      />
    </div>
  );
}
