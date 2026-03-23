import { memo, useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { cn } from '@/shared/lib/utils';
import type { FolderDto } from '../types';
import { useFolders } from '../hooks/useFolders';

interface FolderTreeProps {
  selectedFolderId?: string;
  onSelect: (folderId: string | undefined) => void;
}

interface FolderNodeProps {
  folder: FolderDto;
  selectedFolderId?: string;
  onSelect: (folderId: string | undefined) => void;
}

const FolderNode = memo(function FolderNode({
  folder,
  selectedFolderId,
  onSelect,
}: FolderNodeProps) {
  const { t } = useTranslation('documents');
  const [expanded, setExpanded] = useState(false);
  const { data: children } = useFolders(expanded ? folder.id : undefined);
  const isSelected = selectedFolderId === folder.id;

  const handleToggle = useCallback(() => setExpanded((prev) => !prev), []);
  const handleSelect = useCallback(() => onSelect(folder.id), [onSelect, folder.id]);

  return (
    <div className="ps-4">
      <div className="flex items-center gap-1">
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="h-6 w-6 p-0"
          onClick={handleToggle}
          aria-expanded={expanded}
          aria-label={expanded ? t('lockey_documents_folders_collapse') : t('lockey_documents_folders_expand')}
        >
          {expanded ? '▼' : '▶'}
        </Button>
        <button
          type="button"
          className={cn('text-sm hover:underline', isSelected && 'font-semibold text-primary')}
          onClick={handleSelect}
        >
          {folder.name}
        </button>
      </div>
      {expanded && children && children.length > 0 && (
        <div>
          {children.map((child) => (
            <FolderNode
              key={child.id}
              folder={child}
              selectedFolderId={selectedFolderId}
              onSelect={onSelect}
            />
          ))}
        </div>
      )}
    </div>
  );
});

export function FolderTree({ selectedFolderId, onSelect }: FolderTreeProps) {
  const { t } = useTranslation('documents');
  const { data: rootFolders, isPending } = useFolders();

  const handleSelectRoot = useCallback(() => onSelect(undefined), [onSelect]);

  if (isPending) {
    return <div className="text-sm text-muted-foreground">{t('lockey_common_loading', { ns: 'common' })}</div>;
  }

  return (
    <div className="space-y-1">
      <button
        type="button"
        className={cn('text-sm hover:underline', !selectedFolderId && 'font-semibold text-primary')}
        onClick={handleSelectRoot}
      >
        {t('lockey_documents_folders_root')}
      </button>
      {rootFolders?.map((folder) => (
        <FolderNode
          key={folder.id}
          folder={folder}
          selectedFolderId={selectedFolderId}
          onSelect={onSelect}
        />
      ))}
    </div>
  );
}
