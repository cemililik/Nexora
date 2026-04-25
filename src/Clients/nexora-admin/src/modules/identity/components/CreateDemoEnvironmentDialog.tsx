import { useRef, useState } from 'react';
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

  // AbortController kept in a ref so closing the dialog mid-submit aborts
  // the in-flight request → axios rejects → mutation goes to error state
  // → onSuccess never fires → setResult cannot leak stale data into a
  // dialog the user has already dismissed and may re-open. The controller
  // is recreated per submit so a previous abort does not poison the next
  // one. Note: a fully-form-driven implementation (React Hook Form + Zod
  // per project standard) would offer the same guarantee + validation —
  // intentionally not adopted here because the dialog has a single
  // controlled field (scenario dropdown) and no field-level error surface,
  // so the RHF/Zod ceremony would outweigh the value. Re-evaluate when
  // T-007a turns the dropdown into a server-fetched list with per-row
  // validation requirements.
  const abortRef = useRef<AbortController | null>(null);

  const reset = () => {
    abortRef.current?.abort();
    abortRef.current = null;
    setScenario('general');
    setResult(null);
    mutation.reset();
  };

  const handleSubmit = () => {
    // `scenario` is typed as DemoScenario (non-nullable union) with a
    // hardcoded default of 'general' and `setScenario` only accepts the
    // same union, so a falsy check is dead. Submit unconditionally — if the
    // dropdown ever becomes registry-driven (T-007a), widen the type to
    // `DemoScenario | ''` and reinstate the guard alongside the first
    // Select.Empty item.
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    mutation.mutate(
      { tenantId, scenario, signal: controller.signal },
      {
        onSuccess: (data) => {
          // Guard: if the user closed the dialog while the request was
          // in flight, controller.signal.aborted is true and we must
          // not push stale state.
          if (controller.signal.aborted) return;
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
      case 'Failed': {
        // Backend returns raw `lockey_` keys — resolve via i18n so the UI
        // surfaces the translated message, not the key string. Only call
        // `t(...)` when errorMessage is truthy; appending a trailing
        // " — undefined" if the backend omitted the message would be ugly.
        const base = t('lockey_identity_tenants_demo_outcome_failed');
        if (!errorMessage) return base;
        return `${base} — ${t(errorMessage)}`;
      }
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
              onValueChange={(next) => {
                setScenario(next as DemoScenario);
              }}
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
