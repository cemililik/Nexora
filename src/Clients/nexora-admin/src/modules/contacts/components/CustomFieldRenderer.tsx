import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Check, Pencil, X } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import type { ContactCustomFieldDto, CustomFieldDefinitionDto } from '../types';

interface CustomFieldRendererProps {
  fields: ContactCustomFieldDto[];
  definitions: CustomFieldDefinitionDto[];
  onUpdate: (definitionId: string, value: string) => void;
}

export function CustomFieldRenderer({
  fields,
  definitions,
  onUpdate,
}: CustomFieldRendererProps) {
  const { t } = useTranslation('contacts');
  const [editingFieldId, setEditingFieldId] = useState<string | null>(null);
  const [editValue, setEditValue] = useState('');

  const activeDefinitions = definitions
    .filter((d) => d.isActive)
    .sort((a, b) => a.displayOrder - b.displayOrder);

  function getFieldValue(definitionId: string): string {
    return fields.find((f) => f.fieldDefinitionId === definitionId)?.value ?? '';
  }

  function handleStartEdit(definitionId: string) {
    setEditingFieldId(definitionId);
    setEditValue(getFieldValue(definitionId));
  }

  function handleSave(definitionId: string) {
    onUpdate(definitionId, editValue);
    setEditingFieldId(null);
    setEditValue('');
  }

  function handleCancel() {
    setEditingFieldId(null);
    setEditValue('');
  }

  function parseOptions(options?: string): string[] {
    if (!options) return [];
    try {
      const parsed: unknown = JSON.parse(options);
      if (!Array.isArray(parsed)) return [];
      return parsed.filter((item): item is string => typeof item === 'string');
    } catch {
      return options.split(',').map((o) => o.trim());
    }
  }

  if (activeDefinitions.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        {t('lockey_contacts_custom_fields_empty')}
      </p>
    );
  }

  return (
    <div className="space-y-3">
      {activeDefinitions.map((definition) => {
        const currentValue = getFieldValue(definition.id);
        const isEditing = editingFieldId === definition.id;

        return (
          <div
            key={definition.id}
            className="flex items-start justify-between rounded-md border p-3"
          >
            <div className="flex-1 space-y-1">
              <p className="text-sm font-medium">
                {definition.fieldName}
                {definition.isRequired && (
                  <span className="text-destructive"> *</span>
                )}
              </p>

              {isEditing ? (
                <div className="flex items-center gap-2">
                  {renderEditInput(definition, editValue, setEditValue, parseOptions)}
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => handleSave(definition.id)}
                    aria-label={t('lockey_common_save', { ns: 'common' })}
                  >
                    <Check className="h-4 w-4" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={handleCancel}
                    aria-label={t('lockey_common_cancel', { ns: 'common' })}
                  >
                    <X className="h-4 w-4" />
                  </Button>
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">
                  {currentValue || t('lockey_contacts_custom_fields_not_set')}
                </p>
              )}
            </div>

            {!isEditing && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => handleStartEdit(definition.id)}
                aria-label={t('lockey_contacts_custom_fields_edit')}
              >
                <Pencil className="h-4 w-4" />
              </Button>
            )}
          </div>
        );
      })}
    </div>
  );
}

function renderEditInput(
  definition: CustomFieldDefinitionDto,
  value: string,
  onChange: (value: string) => void,
  parseOptions: (options?: string) => string[],
) {
  const fieldType = definition.fieldType.toLowerCase();

  switch (fieldType) {
    case 'dropdown':
    case 'select': {
      const options = parseOptions(definition.options);
      return (
        <select
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className="flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
        >
          <option value="" />
          {options.map((opt) => (
            <option key={opt} value={opt}>
              {opt}
            </option>
          ))}
        </select>
      );
    }

    case 'date':
      return (
        <Input
          type="date"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className="h-9"
        />
      );

    case 'number':
      return (
        <Input
          type="number"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className="h-9"
        />
      );

    case 'boolean':
    case 'checkbox':
      return (
        <input
          type="checkbox"
          checked={value === 'true'}
          onChange={(e) => onChange(String(e.target.checked))}
          className="h-4 w-4 rounded border-gray-300"
        />
      );

    case 'textarea':
      return (
        <textarea
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className="flex min-h-[60px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
          rows={2}
        />
      );

    case 'text':
    default:
      return (
        <Input
          type="text"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className="h-9"
        />
      );
  }
}
