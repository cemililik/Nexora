import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { Button } from '@/shared/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { FormField } from '@/shared/components/data/FormField';
import {
  useCreateDemoEnvironment,
  type DemoSeedRunResult,
} from '../hooks/useCreateDemoEnvironment';

export interface CreateDemoEnvironmentDialogProps {
  tenantId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * T-008 admin UI: platform-operator dialog that seeds demo data into an
 * existing tenant. Permission guard (`platform.tenants.create_demo`) is
 * enforced on the backend; the caller is responsible for hiding the trigger
 * button if the user lacks the permission. Scenario list is hardcoded to the
 * two foundation scenarios from T-007 (`general`, `ngo`); when T-007 extends
 * the catalogue, wire the dropdown to a server-side enumeration and remove
 * the static list here.
 *
 * On successful submit, the dialog switches to a per-module outcome view so
 * the operator can see which modules seeded, which were already seeded, and
 * which failed — mirrors the `nexora demo:load` CLI summary (T-006).
 */
export function CreateDemoEnvironmentDialog({
  tenantId,
  open,
  onOpenChange,
}: CreateDemoEnvironmentDialogProps) {
  const { t } = useTranslation('identity');
  const [scenario, setScenario] = useState<string>('general');
  const [result, setResult] = useState<DemoSeedRunResult | null>(null);

  const mutation = useCreateDemoEnvironment();

  const reset = () => {
    setScenario('general');
    setResult(null);
    mutation.reset();
  };

  const handleSubmit = () => {
    if (!scenario) return;
    mutation.mutate(
      { tenantId, scenario },
      {
        onSuccess: (data) => setResult(data),
      },
    );
  };

  const renderOutcomeLabel = (status: string, errorMessage?: string | null) => {
    switch (status) {
      case 'Seeded':
        return t('lockey_platform_tenants_demo_outcome_seeded');
      case 'AlreadySeeded':
        return t('lockey_platform_tenants_demo_outcome_already_seeded');
      case 'NoOp':
        return t('lockey_platform_tenants_demo_outcome_noop');
      case 'Failed':
        return `${t('lockey_platform_tenants_demo_outcome_failed')}${errorMessage ? ' — ' + errorMessage : ''}`;
      default:
        return status;
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) reset();
        onOpenChange(next);
      }}
    >
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t('lockey_platform_tenants_demo_dialog_title')}</DialogTitle>
          <DialogDescription>
            {t('lockey_platform_tenants_demo_dialog_description')}
          </DialogDescription>
        </DialogHeader>

        {result === null ? (
          <FormField
            label={t('lockey_platform_tenants_demo_scenario_label')}
            required
            htmlFor="demo-scenario-select"
          >
            <Select value={scenario} onValueChange={setScenario}>
              <SelectTrigger id="demo-scenario-select">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="general">
                  {t('lockey_platform_tenants_demo_scenario_general')}
                </SelectItem>
                <SelectItem value="ngo">
                  {t('lockey_platform_tenants_demo_scenario_ngo')}
                </SelectItem>
              </SelectContent>
            </Select>
          </FormField>
        ) : (
          <div
            className="space-y-1 max-h-60 overflow-y-auto"
            role="list"
            aria-label={t('lockey_platform_tenants_demo_dialog_title')}
          >
            {result.modules.map((m) => (
              <div
                key={m.moduleName}
                role="listitem"
                className="flex items-center justify-between rounded-md border px-3 py-2 text-sm"
              >
                <span className="font-medium">{m.moduleName}</span>
                <span
                  className={
                    m.status === 'Failed'
                      ? 'text-destructive'
                      : m.status === 'Seeded'
                        ? 'text-primary'
                        : 'text-muted-foreground'
                  }
                >
                  {renderOutcomeLabel(m.status, m.errorMessage)}
                </span>
              </div>
            ))}
          </div>
        )}

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => {
              reset();
              onOpenChange(false);
            }}
          >
            {t('lockey_identity_user_link_contact_cancel')}
          </Button>
          {result === null ? (
            <Button onClick={handleSubmit} disabled={mutation.isPending || !scenario}>
              {t('lockey_platform_tenants_demo_submit')}
            </Button>
          ) : null}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default CreateDemoEnvironmentDialog;
