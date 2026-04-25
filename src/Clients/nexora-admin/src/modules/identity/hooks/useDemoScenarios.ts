import { useQuery } from '@tanstack/react-query';

import { api } from '@/shared/lib/api';

/**
 * Server-side scenario shape returned by `GET /identity/demo/scenarios`
 * (T-029). Mirrors the backend `DemoScenarioDto`.
 */
export interface DemoScenarioDto {
  name: string;
  descriptionLockey: string;
  requiredModules: string[];
  optionalModules: string[];
}

export const demoScenarioKeys = {
  all: ['identity', 'demo-scenarios'] as const,
};

/**
 * Loads the catalogue of demo scenarios applicable to the current
 * tenant. Empty array is a legitimate response (no installed module
 * supports a scenario); the dialog renders an EmptyState in that case.
 *
 * Used by `CreateDemoEnvironmentDialog` to populate its scenario
 * dropdown registry-driven instead of carrying a hardcoded list — see
 * T-029 for the split-from-T-007 design rationale.
 */
export function useDemoScenarios() {
  return useQuery<DemoScenarioDto[]>({
    queryKey: demoScenarioKeys.all,
    queryFn: () => api.get<DemoScenarioDto[]>('/identity/demo/scenarios'),
    // Catalogue rarely changes (only when modules install/uninstall) —
    // a 5-minute stale window keeps repeated dialog opens snappy
    // without missing module-state changes for too long.
    staleTime: 5 * 60 * 1000,
  });
}
