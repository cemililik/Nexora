import NextAuth, { type NextAuthConfig } from 'next-auth';
import KeycloakProvider from 'next-auth/providers/keycloak';

declare module 'next-auth' {
  interface Session {
    accessToken?: string;
    tenantId?: string;
    organizationId?: string;
    permissions?: string[];
  }
}

declare module '@auth/core/jwt' {
  interface JWT {
    accessToken?: string;
    refreshToken?: string;
    expiresAt?: number;
    tenantId?: string;
    organizationId?: string;
    permissions?: string[];
  }
}

function decodeJwtPayload(token: string): Record<string, unknown> {
  const parts = token.split('.');
  if (parts.length !== 3) return {};
  const payload = Buffer.from(parts[1], 'base64').toString('utf-8');
  return JSON.parse(payload) as Record<string, unknown>;
}

const authConfig: NextAuthConfig = {
  providers: [
    KeycloakProvider({
      clientId: process.env.AUTH_KEYCLOAK_ID!,
      clientSecret: process.env.AUTH_KEYCLOAK_SECRET!,
      issuer: process.env.AUTH_KEYCLOAK_ISSUER!,
    }),
  ],
  pages: {
    signIn: '/auth/login',
  },
  callbacks: {
    async jwt({ token, account }) {
      if (account?.access_token) {
        token.accessToken = account.access_token;
        token.refreshToken = account.refresh_token;
        token.expiresAt = account.expires_at;

        const claims = decodeJwtPayload(account.access_token);
        token.tenantId = claims.tenant_id as string | undefined;
        token.organizationId = claims.organization_id as string | undefined;
        token.permissions = (claims.permissions as string[]) ?? [];
      }

      // Token refresh: if expired, attempt refresh
      if (token.expiresAt && Date.now() / 1000 > token.expiresAt) {
        try {
          const issuer = process.env.AUTH_KEYCLOAK_ISSUER!;
          const response = await fetch(`${issuer}/protocol/openid-connect/token`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: new URLSearchParams({
              client_id: process.env.AUTH_KEYCLOAK_ID!,
              client_secret: process.env.AUTH_KEYCLOAK_SECRET!,
              grant_type: 'refresh_token',
              refresh_token: token.refreshToken ?? '',
            }),
          });

          if (response.ok) {
            const data = await response.json();
            token.accessToken = data.access_token;
            token.refreshToken = data.refresh_token ?? token.refreshToken;
            token.expiresAt = Math.floor(Date.now() / 1000) + data.expires_in;

            const claims = decodeJwtPayload(data.access_token);
            token.tenantId = claims.tenant_id as string | undefined;
            token.organizationId = claims.organization_id as string | undefined;
            token.permissions = (claims.permissions as string[]) ?? [];
          }
        } catch {
          // Refresh failed — session will be invalidated on next request
          token.accessToken = undefined;
        }
      }

      return token;
    },
    async session({ session, token }) {
      session.accessToken = token.accessToken;
      session.tenantId = token.tenantId;
      session.organizationId = token.organizationId;
      session.permissions = token.permissions;
      return session;
    },
  },
  session: {
    strategy: 'jwt',
  },
};

export const { handlers, signIn, signOut, auth } = NextAuth(authConfig);
