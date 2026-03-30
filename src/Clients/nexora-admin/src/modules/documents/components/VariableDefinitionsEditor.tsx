import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Plus, Trash2 } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Checkbox } from '@/shared/components/ui/checkbox';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';

const VARIABLE_TYPES = ['String', 'Number', 'Date', 'Boolean'] as const;
type VariableType = (typeof VARIABLE_TYPES)[number];

interface VariableDefinition {
  id: string;
  name: string;
  type: VariableType;
  required: boolean;
}

interface VariableDefinitionsEditorProps {
  value: string;
  onChange: (value: string) => void;
}

function parseDefinitions(json: string): VariableDefinition[] {
  if (!json) return [];
  try {
    const parsed: unknown = JSON.parse(json);
    if (Array.isArray(parsed)) {
      return parsed
        .filter(
          (item): item is VariableDefinition =>
            typeof item === 'object' &&
            item !== null &&
            typeof (item as Record<string, unknown>).name === 'string' &&
            typeof (item as Record<string, unknown>).type === 'string' &&
            (VARIABLE_TYPES as readonly string[]).includes(
              (item as Record<string, unknown>).type as string,
            ),
        )
        .map((item) => ({
          ...item,
          id: item.id || crypto.randomUUID(),
        }));
    }
  } catch {
    // Invalid JSON — start empty
  }
  return [];
}

function serializeDefinitions(defs: VariableDefinition[]): string {
  if (defs.length === 0) return '';
  return JSON.stringify(defs);
}

export function VariableDefinitionsEditor({ value, onChange }: VariableDefinitionsEditorProps) {
  const { t } = useTranslation('documents');
  const [definitions, setDefinitions] = useState<VariableDefinition[]>(() =>
    parseDefinitions(value),
  );

  // Sync external value changes
  useEffect(() => {
    const parsed = parseDefinitions(value);
    const serialized = serializeDefinitions(parsed);
    const currentSerialized = serializeDefinitions(definitions);
    if (serialized !== currentSerialized) {
      setDefinitions(parsed);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- `definitions` is intentionally excluded to avoid infinite re-render loops; we only want to sync when the external `value` prop changes
  }, [value]);

  const emitChange = useCallback(
    (defs: VariableDefinition[]) => {
      setDefinitions(defs);
      onChange(serializeDefinitions(defs));
    },
    [onChange],
  );

  const addVariable = () => {
    emitChange([...definitions, { id: crypto.randomUUID(), name: '', type: 'String', required: false }]);
  };

  const removeVariable = (index: number) => {
    emitChange(definitions.filter((_, i) => i !== index));
  };

  const updateVariable = (index: number, updates: Partial<VariableDefinition>) => {
    emitChange(definitions.map((def, i) => (i === index ? { ...def, ...updates } : def)));
  };

  return (
    <div className="space-y-3">
      {definitions.map((def, index) => (
        <div key={def.id} className="flex items-center gap-2">
          <Input
            value={def.name}
            onChange={(e) => updateVariable(index, { name: e.target.value })}
            placeholder={t('lockey_documents_templates_var_name')}
            className="flex-1"
          />
          <Select
            value={def.type}
            onValueChange={(val) => {
              if ((VARIABLE_TYPES as readonly string[]).includes(val)) {
                updateVariable(index, { type: val as VariableType });
              }
            }}
          >
            <SelectTrigger className="w-32" aria-label={t('lockey_documents_templates_var_type')}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {VARIABLE_TYPES.map((vt) => (
                <SelectItem key={vt} value={vt}>
                  {t(`lockey_documents_templates_var_type_${vt.toLowerCase()}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <div className="flex items-center gap-1.5">
            <Checkbox
              checked={def.required}
              onCheckedChange={(checked) =>
                updateVariable(index, { required: checked === true })
              }
              id={`var-required-${index}`}
            />
            <label htmlFor={`var-required-${index}`} className="text-sm">
              {t('lockey_documents_templates_var_required')}
            </label>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => removeVariable(index)}
          >
            <Trash2 className="h-4 w-4 text-destructive" />
          </Button>
        </div>
      ))}
      <Button type="button" variant="outline" size="sm" onClick={addVariable}>
        <Plus className="mr-1 h-4 w-4" />
        {t('lockey_documents_templates_var_add')}
      </Button>
    </div>
  );
}
