import { useCallback, useRef, useState } from 'react';
import type { AxiosError } from 'axios';

import { api } from '@/shared/lib/api';
import { useGenerateUploadUrl } from './useDocuments';
import type { ConfirmUploadRequest, ConfirmUploadResultDto, UploadUrlDto } from '../types';

type UploadState = 'idle' | 'generating-url' | 'uploading' | 'confirming' | 'done' | 'error';

interface UseFileUploadReturn {
  upload: (file: File, meta: Omit<ConfirmUploadRequest, 'storageKey' | 'mimeType' | 'fileSize'>) => Promise<void>;
  state: UploadState;
  progress: number;
  error: string | null;
  documentId: string | null;
  isVersionUpdate: boolean;
  reset: () => void;
}

/**
 * Hook for uploading files via presigned URL flow.
 *
 * The returned `error` value is a `lockey_` translation key suitable for
 * passing directly to `t()` for user-facing error messages.
 */
export function useFileUpload(): UseFileUploadReturn {
  const [state, setState] = useState<UploadState>('idle');
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [documentId, setDocumentId] = useState<string | null>(null);
  const [isVersionUpdate, setIsVersionUpdate] = useState(false);
  const xhrRef = useRef<XMLHttpRequest | null>(null);
  const generateUrl = useGenerateUploadUrl();

  const reset = useCallback(() => {
    if (xhrRef.current) {
      xhrRef.current.abort();
      xhrRef.current = null;
    }
    setState('idle');
    setProgress(0);
    setError(null);
    setDocumentId(null);
    setIsVersionUpdate(false);
  }, []);

  const upload = useCallback(
    async (
      file: File,
      meta: Omit<ConfirmUploadRequest, 'storageKey' | 'mimeType' | 'fileSize'>,
    ) => {
      try {
        reset();

        // Phase 1: Generate presigned upload URL
        setState('generating-url');
        const uploadUrlData: UploadUrlDto = await generateUrl.mutateAsync({
          fileName: file.name,
          contentType: file.type,
          fileSize: file.size,
        });

        // Phase 2: Upload file via XHR to presigned URL
        setState('uploading');
        await new Promise<void>((resolve, reject) => {
          const xhr = new XMLHttpRequest();
          xhrRef.current = xhr;

          xhr.upload.onprogress = (event) => {
            if (event.lengthComputable) {
              setProgress(Math.round((event.loaded / event.total) * 100));
            }
          };

          xhr.onload = () => {
            if (xhr.status >= 200 && xhr.status < 300) {
              resolve();
            } else {
              reject(new Error(`Upload failed with status ${xhr.status}`));
            }
          };

          xhr.onerror = () => reject(new Error('Upload failed'));
          xhr.onabort = () => reject(new Error('Upload aborted'));

          xhr.open('PUT', uploadUrlData.uploadUrl);
          xhr.setRequestHeader('Content-Type', file.type);
          xhr.send(file);
        });

        // Phase 3: Confirm upload
        setState('confirming');
        const confirmData: ConfirmUploadRequest = {
          ...meta,
          storageKey: uploadUrlData.storageKey,
          mimeType: file.type,
          fileSize: file.size,
        };

        const envelope = await api.postRaw<ConfirmUploadResultDto>(
          '/documents/documents/confirm-upload',
          confirmData,
        );

        const result = envelope.data;
        if (result?.document) {
          setDocumentId(result.document.id);
        }

        // Use structured boolean from backend instead of string matching
        setIsVersionUpdate(result?.isVersionUpdate ?? false);

        setState('done');
        setProgress(100);
      } catch (err) {
        setState('error');
        if ((err as AxiosError).isAxiosError === true || (err instanceof Error && 'response' in err)) {
          setError('lockey_error_api');
        } else if (err instanceof Error) {
          setError(err.message.startsWith('lockey_') ? err.message : 'lockey_error_network');
        } else {
          setError('lockey_error_unexpected');
        }
      }
    },
    [generateUrl, reset],
  );

  return { upload, state, progress, error, documentId, isVersionUpdate, reset };
}
