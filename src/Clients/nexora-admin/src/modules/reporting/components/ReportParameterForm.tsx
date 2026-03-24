import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';

interface ParameterDefinition {
  name: string;
  type: string;
  required: boolean;
  defaultValue?: string;
}

interface ReportParameterFormProps {
  parameters: ParameterDefinition[];
  values: Record<string, string>;
  onChange: (values: Record<string, string>) => void;
  onSubmit: () => void;
  isLoading?: boolean;
}

export function ReportParameterForm({
  parameters,
  values,
  onChange,
  onSubmit,
  isLoading,
}: ReportParameterFormProps) {
  const { t } = useTranslation('reporting');

  if (parameters.length === 0) return null;

  return (
    <div className="space-y-4">
      <h3 className="text-sm font-medium text-foreground">
        {t('lockey_reporting_parameters')}
      </h3>
      {parameters.map((param) => (
        <div key={param.name} className="space-y-1">
          <label className="text-sm text-muted-foreground">
            {param.name}
            {param.required && <span className="text-destructive"> *</span>}
          </label>
          <Input
            type={param.type === 'Number' ? 'number' : param.type === 'Date' ? 'date' : 'text'}
            value={values[param.name] ?? param.defaultValue ?? ''}
            onChange={(e) =>
              onChange({ ...values, [param.name]: e.target.value })
            }
            placeholder={param.defaultValue ?? ''}
          />
        </div>
      ))}
      <Button onClick={onSubmit} disabled={isLoading} size="sm">
        {t('lockey_reporting_action_execute')}
      </Button>
    </div>
  );
}
