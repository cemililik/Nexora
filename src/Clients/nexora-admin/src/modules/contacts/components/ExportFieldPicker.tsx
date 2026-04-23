import { useTranslation } from 'react-i18next';

import { Checkbox } from '@/shared/components/ui/checkbox';

export interface ExportFieldPickerProps {
  selectedFields: string[];
  onChange: (fields: string[]) => void;
}

const CORE_FIELDS = [
  'firstName',
  'lastName',
  'email',
  'phone',
  'mobile',
  'website',
  'companyName',
  'taxId',
  'title',
  'type',
  'status',
  'source',
  'language',
  'currency',
  'createdAt',
  'updatedAt',
] as const;

type CoreField = (typeof CORE_FIELDS)[number];

function labelKeyFor(field: CoreField): string {
  return `lockey_contacts_export_field_${field.toLowerCase()}`;
}

export function ExportFieldPicker({ selectedFields, onChange }: ExportFieldPickerProps) {
  const { t } = useTranslation('contacts');

  const toggle = (field: string, checked: boolean) => {
    if (checked) {
      if (selectedFields.includes(field)) return;
      onChange([...selectedFields, field]);
    } else {
      onChange(selectedFields.filter((f) => f !== field));
    }
  };

  return (
    <fieldset className="space-y-2">
      <legend className="text-sm font-medium">
        {t('lockey_contacts_export_form_fields')}
      </legend>
      <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
        {CORE_FIELDS.map((field) => {
          const id = `export-field-${field}`;
          const checked = selectedFields.includes(field);
          return (
            <label
              key={field}
              htmlFor={id}
              className="flex items-center gap-2 text-sm"
            >
              <Checkbox
                id={id}
                checked={checked}
                onCheckedChange={(value) => toggle(field, value === true)}
              />
              <span>{t(labelKeyFor(field))}</span>
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}

export default ExportFieldPicker;
