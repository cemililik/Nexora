import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Input } from '@/shared/components/ui/input';
import { Checkbox } from '@/shared/components/ui/checkbox';

interface VariableDefinition {
  name: string;
  type: 'String' | 'Number' | 'Date' | 'Boolean';
  required: boolean;
}

interface VariableEditorProps {
  variableDefinitions: string;
  value: string;
  onChange: (value: string) => void;
}

function parseDefinitions(json: string): VariableDefinition[] {
  if (!json) return [];
  try {
    const parsed: unknown = JSON.parse(json);
    if (Array.isArray(parsed)) {
      return parsed.filter(
        (item): item is VariableDefinition =>
          typeof item === 'object' &&
          item !== null &&
          typeof (item as Record<string, unknown>).name === 'string' &&
          typeof (item as Record<string, unknown>).type === 'string',
      );
    }
  } catch {
    // Invalid definitions JSON
  }
  return [];
}

function parseValues(json: string): Record<string, string> {
  if (!json) return {};
  try {
    const parsed: unknown = JSON.parse(json);
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, string>;
    }
  } catch {
    // Invalid values JSON
  }
  return {};
}

export function VariableEditor({ variableDefinitions, value, onChange }: VariableEditorProps) {
  const { t } = useTranslation('documents');
  const definitions = parseDefinitions(variableDefinitions);
  const [values, setValues] = useState<Record<string, string>>(() => parseValues(value));

  useEffect(() => {
    const parsed = parseValues(value);
    setValues(parsed);
  }, [value]);

  const emitChange = useCallback(
    (newValues: Record<string, string>) => {
      setValues(newValues);
      onChange(JSON.stringify(newValues));
    },
    [onChange],
  );

  const updateValue = (name: string, val: string) => {
    emitChange({ ...values, [name]: val });
  };

  if (definitions.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        {t('lockey_documents_templates_var_no_definitions')}
      </p>
    );
  }

  return (
    <div className="space-y-3">
      {definitions.map((def) => (
        <div key={def.name}>
          <label className="text-sm font-medium">
            {def.name}
            {def.required && <span className="ml-1 text-destructive">*</span>}
          </label>
          {def.type === 'Boolean' ? (
            <div className="mt-1 flex items-center gap-2">
              <Checkbox
                checked={values[def.name] === 'true'}
                onCheckedChange={(checked) =>
                  updateValue(def.name, checked === true ? 'true' : 'false')
                }
                id={`var-${def.name}`}
              />
              <label htmlFor={`var-${def.name}`} className="text-sm">
                {def.name}
              </label>
            </div>
          ) : (
            <Input
              type={
                def.type === 'Number'
                  ? 'number'
                  : def.type === 'Date'
                    ? 'date'
                    : 'text'
              }
              value={values[def.name] ?? ''}
              onChange={(e) => updateValue(def.name, e.target.value)}
              className="mt-1"
              required={def.required}
            />
          )}
        </div>
      ))}
    </div>
  );
}
