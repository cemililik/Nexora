import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { CheckCircle2 } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Textarea } from '@/shared/components/ui/textarea';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/shared/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { useFolders } from '../hooks/useFolders';
import { useFileUpload } from '../hooks/useFileUpload';
import { FileDropZone } from '../components/FileDropZone';

function createUploadSchema(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    folderId: z.string().min(1, t('lockey_validation_required', { ns: 'validation' })),
    name: z.string().min(1, t('lockey_validation_required', { ns: 'validation' })),
    description: z.string().optional(),
  });
}

type UploadFormValues = z.infer<ReturnType<typeof createUploadSchema>>;

export default function DocumentUploadPage() {
  const { t } = useTranslation('documents');
  const navigate = useNavigate();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const canUpload = hasPermission('documents.document.upload');

  const { data: folders } = useFolders();
  const { upload, state, progress, error, documentId, isVersionUpdate, reset } = useFileUpload();

  const [file, setFile] = useState<File | null>(null);

  const schema = useMemo(() => createUploadSchema(t), [t]);
  const form = useForm<UploadFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { folderId: '', name: '', description: '' },
  });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_documents_module_name' },
      { label: 'lockey_documents_nav_documents', path: '/documents/documents' },
      { label: 'lockey_documents_upload_title' },
    ]);
  }, [setBreadcrumbs]);

  const handleFileSelect = (f: File) => {
    setFile(f);
    // Auto-fill name from file name (without extension)
    const baseName = f.name.replace(/\.[^/.]+$/, '');
    form.setValue('name', baseName);
  };

  const handleUpload = form.handleSubmit(async (values: UploadFormValues) => {
    if (!file) return;
    await upload(file, {
      folderId: values.folderId,
      name: values.name || file.name,
      description: values.description || undefined,
    });
  });

  const handleReset = () => {
    reset();
    setFile(null);
    form.reset();
  };

  if (!canUpload) {
    return (
      <div className="flex items-center justify-center p-12">
        <p className="text-muted-foreground">{t('lockey_documents_upload_no_permission')}</p>
      </div>
    );
  }

  const isUploading = state === 'generating-url' || state === 'uploading' || state === 'confirming';
  const isDone = state === 'done';
  const isError = state === 'error';

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">{t('lockey_documents_upload_title')}</h1>

      <Card className="max-w-2xl">
        <CardHeader>
          <CardTitle className="text-lg">{t('lockey_documents_upload_title')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {isDone ? (
            <div className="space-y-4">
              <div className="flex items-center gap-2 text-green-600">
                <CheckCircle2 className="h-5 w-5" />
                <p className="font-medium">
                  {isVersionUpdate
                    ? t('lockey_documents_upload_success_version')
                    : t('lockey_documents_upload_success_new')}
                </p>
              </div>
              <div className="flex gap-2">
                {documentId && (
                  <Button
                    variant="outline"
                    onClick={() => navigate(`/documents/documents/${documentId}`)}
                  >
                    {t('lockey_documents_upload_view_document')}
                  </Button>
                )}
                <Button variant="outline" onClick={handleReset}>
                  {t('lockey_documents_upload_another')}
                </Button>
                <Button onClick={() => navigate('/documents/documents')}>
                  {t('lockey_documents_nav_documents')}
                </Button>
              </div>
            </div>
          ) : (
            <>
              {/* Folder selector */}
              <div>
                <label className="text-sm font-medium">{t('lockey_documents_form_folder')}</label>
                <Controller
                  control={form.control}
                  name="folderId"
                  render={({ field }) => (
                    <Select value={field.value} onValueChange={field.onChange} disabled={isUploading}>
                      <SelectTrigger className="mt-1" aria-label={t('lockey_documents_form_folder')}>
                        <SelectValue placeholder={t('lockey_documents_filter_all_folders')} />
                      </SelectTrigger>
                      <SelectContent>
                        {folders?.map((f) => (
                          <SelectItem key={f.id} value={f.id}>
                            {f.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                />
                {form.formState.errors.folderId?.message && (
                  <p className="mt-1 text-sm text-destructive">{form.formState.errors.folderId.message}</p>
                )}
              </div>

              {/* File drop zone */}
              <div>
                <label className="text-sm font-medium">{t('lockey_documents_form_file')}</label>
                <div className="mt-1">
                  <FileDropZone onFileSelect={handleFileSelect} disabled={isUploading} />
                </div>
              </div>

              {/* Name */}
              <div>
                <label className="text-sm font-medium">{t('lockey_documents_form_name')}</label>
                <Input
                  {...form.register('name')}
                  className="mt-1"
                  disabled={isUploading}
                />
                {form.formState.errors.name?.message && (
                  <p className="mt-1 text-sm text-destructive">{form.formState.errors.name.message}</p>
                )}
              </div>

              {/* Description */}
              <div>
                <label className="text-sm font-medium">{t('lockey_documents_form_description')}</label>
                <Textarea
                  {...form.register('description')}
                  className="mt-1"
                  rows={3}
                  disabled={isUploading}
                />
              </div>

              {/* Progress bar */}
              {isUploading && (
                <div className="space-y-1">
                  <p className="text-sm text-muted-foreground">
                    {state === 'generating-url' && t('lockey_documents_upload_step_preparing')}
                    {state === 'uploading' && t('lockey_documents_upload_step_upload')}
                    {state === 'confirming' && t('lockey_documents_upload_step_confirm')}
                  </p>
                  <div className="h-2 w-full overflow-hidden rounded-full bg-secondary">
                    <div
                      className="h-full bg-primary transition-all duration-300"
                      style={{ width: `${progress}%` }}
                    />
                  </div>
                  <p className="text-xs text-muted-foreground">{progress}%</p>
                </div>
              )}

              {/* Error */}
              {isError && error && (
                <p className="text-sm text-destructive">{error}</p>
              )}

              {/* Actions */}
              <div className="flex gap-2">
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => navigate('/documents/documents')}
                  disabled={isUploading}
                >
                  {t('lockey_common_cancel', { ns: 'common' })}
                </Button>
                <Button
                  type="button"
                  onClick={handleUpload}
                  disabled={!file || !form.getValues('folderId') || isUploading}
                >
                  {t('lockey_documents_action_upload')}
                </Button>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
