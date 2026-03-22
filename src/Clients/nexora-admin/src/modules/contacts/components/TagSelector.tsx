import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { X } from 'lucide-react';

import { Badge } from '@/shared/components/ui/badge';
import { Button } from '@/shared/components/ui/button';
import type { ContactTagSummaryDto, TagDto } from '../types';

interface TagSelectorProps {
  assignedTags: ContactTagSummaryDto[];
  availableTags: TagDto[];
  onAssign: (tagId: string) => void;
  onRemove: (tagId: string) => void;
}

export function TagSelector({
  assignedTags,
  availableTags,
  onAssign,
  onRemove,
}: TagSelectorProps) {
  const { t } = useTranslation('contacts');
  const [selectedTagId, setSelectedTagId] = useState('');

  const assignedTagIds = new Set(assignedTags.map((tag) => tag.tagId));
  const unassignedTags = availableTags.filter(
    (tag) => tag.isActive && !assignedTagIds.has(tag.id),
  );

  function handleAssign() {
    if (selectedTagId) {
      onAssign(selectedTagId);
      setSelectedTagId('');
    }
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap gap-2">
        {assignedTags.length === 0 && (
          <p className="text-sm text-muted-foreground">
            {t('lockey_contacts_empty_tags')}
          </p>
        )}
        {assignedTags.map((tag) => (
          <Badge
            key={tag.tagId}
            variant="outline"
            className="gap-1"
            // Inline style required: tag color is dynamic data from the backend
            style={
              tag.color
                ? { backgroundColor: `${tag.color}20`, borderColor: tag.color, color: tag.color }
                : undefined
            }
          >
            {tag.name}
            <button
              type="button"
              onClick={() => onRemove(tag.tagId)}
              className="ms-1 rounded-full hover:bg-black/10"
              aria-label={t('lockey_contacts_tags_remove', { name: tag.name })}
            >
              <X className="h-3 w-3" />
            </button>
          </Badge>
        ))}
      </div>

      {unassignedTags.length > 0 && (
        <div className="flex items-center gap-2">
          <select
            value={selectedTagId}
            onChange={(e) => setSelectedTagId(e.target.value)}
            className="flex h-9 w-full max-w-xs rounded-md border border-input bg-background px-3 py-1 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
          >
            <option value="">{t('lockey_contacts_tags_select')}</option>
            {unassignedTags.map((tag) => (
              <option key={tag.id} value={tag.id}>
                {tag.name}
              </option>
            ))}
          </select>
          <Button type="button" size="sm" onClick={handleAssign} disabled={!selectedTagId}>
            {t('lockey_contacts_tags_assign')}
          </Button>
        </div>
      )}
    </div>
  );
}
