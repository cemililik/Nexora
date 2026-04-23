/**
 * Mirrors backend ComplianceKeySummaryDto (Nexora.Host.Endpoints.ComplianceConfigEndpoints).
 * Surfaces each layer's contribution so the admin UI can display "tenant default / org
 * override / effective" + cap badge without extra round-trips.
 *
 * See ADR-0025.
 */
export interface ComplianceKeySummary {
  /** Configuration key (e.g. "gdpr.hard_delete.enabled"). */
  key: string;
  /** Resolved effective value after applying precedence. */
  effectiveValue: boolean;
  /** Tenant default, if set. `null` when unset (no row in `platform_tenant_config`). */
  tenantDefault: boolean | null;
  /** Organization override, if set. `null` when the current org has no override. */
  orgOverride: boolean | null;
  /** Which layer supplied the effective value: None | Cap | TenantDefault | OrgOverride. */
  winningLayer: 'None' | 'Cap' | 'TenantDefault' | 'OrgOverride';
  /** Platform cap: whether the key may be enabled at all. */
  capAllowed: boolean;
  /** Platform cap: whether the cap's own value is forced regardless of lower layers. */
  capForced: boolean;
}

export interface SetComplianceOverrideBody {
  value: boolean;
  reason: string;
}
