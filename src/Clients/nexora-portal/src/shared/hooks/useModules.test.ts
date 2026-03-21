import { describe, expect, it } from 'vitest';

/**
 * Tests for module filtering logic used in useModules hook.
 * Tests the pure logic without React hooks to avoid needing
 * a full provider tree.
 */
describe('module filtering logic', () => {
  interface TenantModuleDto {
    moduleName: string;
    isActive: boolean;
  }

  interface PortalModuleManifest {
    name: string;
  }

  function filterActiveModules(
    installed: TenantModuleDto[],
    registry: PortalModuleManifest[],
  ) {
    const activeNames = new Set(
      installed.filter((m) => m.isActive).map((m) => m.moduleName),
    );
    return registry.filter((m) => activeNames.has(m.name));
  }

  function hasModule(
    installed: TenantModuleDto[],
    moduleName: string,
  ): boolean {
    const activeNames = new Set(
      installed.filter((m) => m.isActive).map((m) => m.moduleName),
    );
    return activeNames.has(moduleName);
  }

  const installedModules: TenantModuleDto[] = [
    { moduleName: 'donations', isActive: true },
    { moduleName: 'crm', isActive: false },
    { moduleName: 'events', isActive: true },
  ];

  const registry: PortalModuleManifest[] = [
    { name: 'donations' },
    { name: 'crm' },
    { name: 'events' },
    { name: 'surveys' },
  ];

  it('should filter registry to only active installed modules', () => {
    const result = filterActiveModules(installedModules, registry);
    expect(result).toHaveLength(2);
    expect(result.map((m) => m.name)).toEqual(['donations', 'events']);
  });

  it('should exclude inactive modules', () => {
    const result = filterActiveModules(installedModules, registry);
    expect(result.map((m) => m.name)).not.toContain('crm');
  });

  it('should exclude modules not in installed list', () => {
    const result = filterActiveModules(installedModules, registry);
    expect(result.map((m) => m.name)).not.toContain('surveys');
  });

  it('should return empty array when no modules installed', () => {
    const result = filterActiveModules([], registry);
    expect(result).toHaveLength(0);
  });

  it('hasModule should return true for active modules', () => {
    expect(hasModule(installedModules, 'donations')).toBe(true);
    expect(hasModule(installedModules, 'events')).toBe(true);
  });

  it('hasModule should return false for inactive modules', () => {
    expect(hasModule(installedModules, 'crm')).toBe(false);
  });

  it('hasModule should return false for non-installed modules', () => {
    expect(hasModule(installedModules, 'surveys')).toBe(false);
  });
});
