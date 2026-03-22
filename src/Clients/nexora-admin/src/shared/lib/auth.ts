import Keycloak from 'keycloak-js';

import type { JwtClaims } from '@/shared/types/auth';

let keycloakInstance: Keycloak | null = null;

/** Create or return the singleton Keycloak instance. */
export function createKeycloak(): Keycloak {
  if (keycloakInstance) return keycloakInstance;

  const url = import.meta.env.VITE_KEYCLOAK_URL as string | undefined;
  const realm = import.meta.env.VITE_KEYCLOAK_REALM as string | undefined;
  const clientId = import.meta.env.VITE_KEYCLOAK_CLIENT_ID as string | undefined;

  const missing = [
    !url && 'VITE_KEYCLOAK_URL',
    !realm && 'VITE_KEYCLOAK_REALM',
    !clientId && 'VITE_KEYCLOAK_CLIENT_ID',
  ].filter(Boolean);

  if (missing.length > 0) {
    throw new Error(
      `Missing required Keycloak environment variables: ${missing.join(', ')}`,
    );
  }

  keycloakInstance = new Keycloak({ url: url!, realm: realm!, clientId: clientId! });

  return keycloakInstance;
}

/** Get the existing Keycloak instance (null if not yet created). */
export function getKeycloak(): Keycloak | null {
  return keycloakInstance;
}

/**
 * Parse JWT claims from a Keycloak access token.
 * The token is already verified by Keycloak JS adapter, so we only decode.
 */
export function parseTokenClaims(token: string): JwtClaims {
  const parts = token.split('.');
  const payload = parts[1];
  if (!payload) {
    throw new Error('Invalid JWT: missing payload segment');
  }

  // Base64url → Base64 → decode
  const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
  const padLen = (4 - base64.length % 4) % 4;
  const padded = base64 + '='.repeat(padLen);
  let decodedBase64: string;
  try {
    decodedBase64 = atob(padded);
  } catch {
    throw new Error('Invalid base64 in JWT payload');
  }
  const jsonPayload = decodeURIComponent(
    decodedBase64
      .split('')
      .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
      .join(''),
  );

  const decoded = JSON.parse(jsonPayload) as Record<string, unknown>;

  return {
    sub: String(decoded.sub ?? ''),
    tenant_id: String(decoded.tenant_id ?? ''),
    organization_id: decoded.organization_id
      ? String(decoded.organization_id)
      : undefined,
    permissions: Array.isArray(decoded.permissions)
      ? (decoded.permissions as string[])
      : [],
    preferred_username: String(decoded.preferred_username ?? ''),
    email: String(decoded.email ?? ''),
    exp: Number(decoded.exp ?? 0),
  };
}
