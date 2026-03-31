import { useCallback, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Upload, X } from 'lucide-react';

import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/components/ui/button';

interface FileDropZoneProps {
  onFileSelect: (file: File) => void;
  accept?: string;
  maxSizeMB?: number;
  disabled?: boolean;
  /** Upload progress percentage (0-100). When provided, a progress bar is shown. */
  progress?: number;
  /** Whether upload is currently in progress. */
  isUploading?: boolean;
}

export function FileDropZone({
  onFileSelect,
  accept,
  maxSizeMB = 100,
  disabled = false,
  progress,
  isUploading = false,
}: FileDropZoneProps) {
  const { t } = useTranslation('documents');
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);

  const validateFile = useCallback(
    (file: File): boolean => {
      setError(null);

      if (accept) {
        const acceptedTypes = accept.split(',').map((t) => t.trim());
        const fileExt = `.${file.name.split('.').pop()?.toLowerCase()}`;
        const matches = acceptedTypes.some(
          (type) =>
            type === file.type ||
            type === fileExt ||
            (type.endsWith('/*') && file.type.startsWith(type.replace('/*', '/'))),
        );
        if (!matches) {
          setError(t('lockey_documents_upload_invalid_type'));
          return false;
        }
      }

      const maxBytes = maxSizeMB * 1024 * 1024;
      if (file.size > maxBytes) {
        setError(t('lockey_documents_upload_file_too_large', { maxSize: maxSizeMB }));
        return false;
      }

      return true;
    },
    [accept, maxSizeMB, t],
  );

  const handleFile = useCallback(
    (file: File) => {
      if (validateFile(file)) {
        setSelectedFile(file);
        onFileSelect(file);
      }
    },
    [validateFile, onFileSelect],
  );

  const handleDragOver = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      if (!disabled) setIsDragging(true);
    },
    [disabled],
  );

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  }, []);

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setIsDragging(false);
      if (disabled) return;

      const file = e.dataTransfer.files[0];
      if (file) handleFile(file);
    },
    [disabled, handleFile],
  );

  const handleInputChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (file) handleFile(file);
      // Reset so re-selecting same file triggers change
      e.target.value = '';
    },
    [handleFile],
  );

  const handleClear = useCallback(() => {
    setSelectedFile(null);
    setError(null);
  }, []);

  const formatSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  return (
    <div className="space-y-2">
      <div
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={() => !disabled && inputRef.current?.click()}
        className={cn(
          'flex cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed p-8 transition-colors',
          isDragging
            ? 'border-primary bg-primary/5'
            : 'border-muted-foreground/25 hover:border-primary/50',
          disabled && 'cursor-not-allowed opacity-50',
        )}
      >
        <Upload className="mb-2 h-8 w-8 text-muted-foreground" />
        <p className="text-sm font-medium text-muted-foreground">
          {t('lockey_documents_upload_drag_or_click')}
        </p>
        <p className="mt-1 text-xs text-muted-foreground">
          {t('lockey_documents_upload_max_size', { maxSize: maxSizeMB })}
        </p>
      </div>

      <input
        ref={inputRef}
        type="file"
        accept={accept}
        onChange={handleInputChange}
        className="hidden"
        disabled={disabled}
      />

      {error && <p className="text-sm text-destructive">{error}</p>}

      {selectedFile && (
        <div className="space-y-1">
          <div className="flex items-center justify-between rounded-md border bg-muted/50 px-3 py-2">
            <span className="truncate text-sm">
              {selectedFile.name} ({formatSize(selectedFile.size)})
            </span>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={(e) => {
                e.stopPropagation();
                handleClear();
              }}
              disabled={disabled || isUploading}
              aria-label={t('lockey_documents_upload_clear')}
            >
              <X className="h-4 w-4" aria-hidden="true" />
            </Button>
          </div>
          {isUploading && progress !== undefined && (
            <div className="relative h-2 w-full overflow-hidden rounded-full bg-muted">
              <div
                className="h-full rounded-full bg-primary transition-all duration-300"
                style={{ width: `${Math.min(progress, 100)}%` }}
              />
            </div>
          )}
        </div>
      )}
    </div>
  );
}
