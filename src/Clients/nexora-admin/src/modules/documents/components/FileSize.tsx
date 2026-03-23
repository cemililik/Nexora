import { useTranslation } from 'react-i18next';

interface FileSizeProps {
  bytes: number;
}

export function FileSize({ bytes }: FileSizeProps) {
  const { t } = useTranslation('documents');

  if (bytes < 1024) {
    return <>{t('lockey_documents_file_size_bytes', { size: bytes })}</>;
  }
  if (bytes < 1024 * 1024) {
    return <>{t('lockey_documents_file_size_kb', { size: (bytes / 1024).toFixed(1) })}</>;
  }
  return <>{t('lockey_documents_file_size_mb', { size: (bytes / (1024 * 1024)).toFixed(1) })}</>;
}
