import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Trash2 } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import type { ContactRelationshipDto } from '../types';

const toSnakeCase = (str: string) => str.replace(/([A-Z])/g, '_$1').toLowerCase().replace(/^_/, '');

interface RelationshipListProps {
  relationships: ContactRelationshipDto[];
  onRemove: (id: string) => void;
}

export function RelationshipList({ relationships, onRemove }: RelationshipListProps) {
  const { t, i18n } = useTranslation('contacts');
  const [removeId, setRemoveId] = useState<string | null>(null);

  function handleConfirmRemove() {
    if (removeId) {
      onRemove(removeId);
      setRemoveId(null);
    }
  }

  if (relationships.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        {t('lockey_contacts_empty_relationships')}
      </p>
    );
  }

  return (
    <div className="space-y-2">
      {relationships.map((rel) => (
        <div
          key={rel.id}
          className="flex items-center justify-between rounded-md border p-3"
        >
          <div className="space-y-1">
            <p className="text-sm font-medium">{rel.relatedContactDisplayName}</p>
            <p className="text-xs text-muted-foreground">
              {t(`lockey_contacts_relationship_type_${toSnakeCase(rel.type)}`)}
              {' '}&middot;{' '}
              {new Date(rel.createdAt).toLocaleDateString(i18n.language)}
            </p>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => setRemoveId(rel.id)}
            aria-label={t('lockey_contacts_relationship_remove')}
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      ))}

      <ConfirmDialog
        open={removeId !== null}
        onOpenChange={(open) => {
          if (!open) setRemoveId(null);
        }}
        title={t('lockey_contacts_relationships_remove_title')}
        description={t('lockey_contacts_relationships_remove_description')}
        onConfirm={handleConfirmRemove}
        variant="destructive"
      />
    </div>
  );
}
