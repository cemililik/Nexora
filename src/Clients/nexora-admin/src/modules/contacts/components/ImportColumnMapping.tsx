import { useTranslation } from 'react-i18next';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';

/** Sentinel value representing a source column that should be skipped. */
export const SKIP_MAPPING = '__skip__';

/** Target Contact fields available for mapping. */
const TARGET_FIELDS = [
  'firstName',
  'lastName',
  'email',
  'phone',
  'mobile',
  'website',
  'companyName',
  'taxId',
  'title',
] as const;

/** Fields that are required — the user must map at least one source column to them. */
const REQUIRED_FIELDS: ReadonlySet<string> = new Set(['email']);

/** Props for {@link ImportColumnMapping}. */
export interface ImportColumnMappingProps {
  /** Detected source file headers. */
  headers: string[];
  /** Current mapping: source header → target field name (or `__skip__`). */
  mapping: Record<string, string>;
  /** Fires with the updated mapping when any row changes. */
  onChange: (mapping: Record<string, string>) => void;
}

/**
 * Column-mapping table for the contact import wizard. Renders one row per
 * detected source header with a shadcn Select to choose the target Contact
 * field (or `__skip__`). Required fields are marked with a red asterisk.
 */
export function ImportColumnMapping({
  headers,
  mapping,
  onChange,
}: ImportColumnMappingProps) {
  const { t } = useTranslation('contacts');

  const setRow = (header: string, value: string) => {
    onChange({ ...mapping, [header]: value });
  };

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full text-sm">
        <thead>
          <tr className="border-b bg-muted/50 text-left">
            <th className="px-3 py-2 font-medium">
              {t('lockey_contacts_import_mapping_source_header')}
            </th>
            <th className="px-3 py-2 font-medium">
              {t('lockey_contacts_import_mapping_target_field')}
            </th>
          </tr>
        </thead>
        <tbody>
          {headers.map((header) => {
            const currentValue = mapping[header] ?? SKIP_MAPPING;
            return (
              <tr key={header} className="border-b hover:bg-muted/30">
                <td className="px-3 py-2 font-mono text-xs">{header}</td>
                <td className="px-3 py-2">
                  <Select
                    value={currentValue}
                    onValueChange={(value) => setRow(header, value)}
                  >
                    <SelectTrigger className="w-full">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={SKIP_MAPPING}>
                        {t('lockey_contacts_import_mapping_skip')}
                      </SelectItem>
                      {TARGET_FIELDS.map((field) => (
                        <SelectItem key={field} value={field}>
                          {t(`lockey_contacts_import_mapping_field_${field.toLowerCase()}`)}
                          {REQUIRED_FIELDS.has(field) && (
                            <span className="ms-1 text-destructive" aria-hidden="true">
                              *
                            </span>
                          )}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
