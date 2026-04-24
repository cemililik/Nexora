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
import { cn } from '@/shared/lib/utils';
import {
  useCreateDemoEnvironment,
  type DemoSeedModuleOutcome,
  type DemoSeedRunResult,
} from '../hooks/useCreateDemoEnvironment';

/**
 * Supported demo-scenario identifiers. Kept as a typed union so TypeScript
 * catches drift when a backend scenario is added but the frontend dropdown
 * is not updated. When T-007a ships the `IDemoScenarioRegistry`, replace
 * this with a fetched list + derived-enum pattern.
 */
export type DemoScenario = 'general' | 'ngo';

const DEMO_SCENARIOS: readonly DemoScenario[] = ['general', 'ngo'] as const;

export interface CreateDemoEnvironmentDialogProps {
  tenantId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * T-008 admin UI: platform-operator dialog that seeds demo data into an
 * existing tenant. Permission guard (`platform.tenants.create_demo`) is
 * enforced on the backend; the caller is responsible for hiding the trigger
 * button if the user lacks the permission. Scenario list is the hardcoded
 * {@link DemoScenario} union (`general`, `ngo`); when T-007a extends the
 * catalogue, wire the dropdown to `IDemoScenarioRegistry` and remove the
 * static union here.
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
  const [scenario, setScenario] = useState<DemoScenario>('general');
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
        onSuccess: (data) => {
          setResult(data);
        },
      },
    );
  };

  const renderOutcomeLabel = (
    status: DemoSeedModuleOutcome['status'],
    errorMessage?: string | null,
  ) => {
    switch (status) {
      case 'Seeded':
        return t('lockey_identity_tenants_demo_outcome_seeded');
      case 'AlreadySeeded':
        return t('lockey_identity_tenants_demo_outcome_already_seeded');
      case 'NoOp':
        return t('lockey_identity_tenants_demo_outcome_noop');
      case 'Failed':
        return `${t('lockey_identity_tenants_demo_outcome_failed')}${errorMessage ? ' — ' + errorMessage : ''}`;
      default:
        return status;
    }
  };

  const outcomeStatusClass = (status: DemoSeedModuleOutcome['status']) => {
    const statusToClass: Record<DemoSeedModuleOutcome['status'], string> = {
      Failed: 'text-destructive',
      Seeded: 'text-primary',
      AlreadySeeded: 'text-muted-foreground',
      NoOp: 'text-muted-foreground',
    };
    return cn(statusToClass[status]);
  };

  const scenarioLabel: Record<DemoScenario, string> = {
    general: t('lockey_identity_tenants_demo_scenario_general'),
    ngo: t('lockey_identity_tenants_demo_scenario_ngo'),
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
          <DialogTitle>{t('lockey_identity_tenants_demo_dialog_title')}</DialogTitle>
          <DialogDescription>
            {t('lockey_identity_tenants_demo_dialog_description')}
          </DialogDescription>
        </DialogHeader>

        {result === null ? (
          <FormField
            label={t('lockey_identity_tenants_demo_scenario_label')}
            required
            htmlFor="demo-scenario-select"
          >
            <Select
              value={scenario}
              onValueChange={(next) => setScenario(next as DemoScenario)}
            >
              <SelectTrigger id="demo-scenario-select">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {DEMO_SCENARIOS.map((s) => (
                  <SelectItem key={s} value={s}>
                    {scenarioLabel[s]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FormField>
        ) : (
          <ul
            className="space-y-1 max-h-60 overflow-y-auto"
            aria-label={t('lockey_identity_tenants_demo_dialog_title')}
          >
            {result.modules.map((m) => (
              <li
                key={m.moduleName}
                className="flex items-center justify-between rounded-md border px-3 py-2 text-sm"
              >
                <span className="font-medium">{m.moduleName}</span>
                <span className={outcomeStatusClass(m.status)}>
                  {renderOutcomeLabel(m.status, m.errorMessage)}
                </span>
              </li>
            ))}
          </ul>
        )}

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => {
              reset();
              onOpenChange(false);
            }}
          >
            {t('lockey_identity_tenants_demo_cancel')}
          </Button>
          {result === null ? (
            <Button onClick={handleSubmit} disabled={mutation.isPending || !scenario}>
              {t('lockey_identity_tenants_demo_submit')}
            </Button>
          ) : null}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default CreateDemoEnvironmentDialog;
