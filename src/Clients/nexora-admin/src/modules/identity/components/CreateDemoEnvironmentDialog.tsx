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
import { EmptyState } from '@/shared/components/feedback/EmptyState';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { cn } from '@/shared/lib/utils';
import {
  useCreateDemoEnvironment,
  type DemoSeedModuleOutcome,
  type DemoSeedRunResult,
} from '../hooks/useCreateDemoEnvironment';
import { useDemoScenarios } from '../hooks/useDemoScenarios';

/**
 * Backend scenario identifier — string-typed because T-029 turned the
 * dropdown registry-driven (server returns the catalogue via
 * `GET /identity/demo/scenarios`). The hardcoded union previously here
 * is removed; new scenarios that ship from a future module's
 * `IDemoScenarioContribution` show up automatically without a frontend
 * change.
 */
export type DemoScenario = string;

export interface CreateDemoEnvironmentDialogProps {
  tenantId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * T-008 admin UI: platform-operator dialog that seeds demo data into an
 * existing tenant. Permission guard (`platform.tenants.create_demo`) is
 * enforced on the backend; the caller is responsible for hiding the trigger
 * button if the user lacks the permission.
 *
 * Scenario catalogue is registry-driven via `useDemoScenarios()` (T-029) —
 * `GET /identity/demo/scenarios` returns the scenarios whose
 * `RequiredModules` are all installed for the current tenant. Empty
 * catalogue is a legitimate state and renders `<EmptyState>`.
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
  const scenariosQuery = useDemoScenarios();
  const [scenario, setScenario] = useState<DemoScenario>('');
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
    setScenario('');
    setResult(null);
    mutation.reset();
  };

  const handleSubmit = () => {
    // Scenario is now empty until the user picks one from the
    // server-fetched list (T-029) — guard the empty case.
    if (!scenario) return;
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

  // Per-scenario label resolution: the server returns the scenario
  // name (kebab-case identifier) + a description lockey. We compose the
  // display string from a name-specific lockey when one exists in the
  // bundle and the description below it. New scenarios from future
  // module contributions just need their name + description lockeys
  // added to the bundle — no code change here.
  const scenarioLabel = (name: string): string => {
    const nameKey = `lockey_identity_tenants_demo_scenario_${name}`;
    return t(nameKey, { defaultValue: name });
  };

  const scenarios = scenariosQuery.data ?? [];
  const isLoadingScenarios = scenariosQuery.isLoading;

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
          isLoadingScenarios ? (
            <LoadingSkeleton lines={2} />
          ) : scenarios.length === 0 ? (
            <EmptyState
              title={t('lockey_identity_tenants_demo_no_scenarios_title')}
              description={t('lockey_identity_tenants_demo_no_scenarios_description')}
            />
          ) : (
          <FormField
            label={t('lockey_identity_tenants_demo_scenario_label')}
            required
            htmlFor="demo-scenario-select"
          >
            <Select
              value={scenario}
              onValueChange={(next) => {
                setScenario(next);
              }}
            >
              <SelectTrigger id="demo-scenario-select">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {scenarios.map((s) => (
                  <SelectItem key={s.name} value={s.name}>
                    {scenarioLabel(s.name)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FormField>
          )
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
          {result === null && scenarios.length > 0 ? (
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
