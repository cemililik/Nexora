'use client';

import Image from 'next/image';

import { cn } from '@/shared/lib/utils';

import { useOrganization } from '@/shared/hooks/useOrganization';

interface TenantLogoProps {
  className?: string;
  size?: number;
}

/**
 * Renders the organization logo. Falls back to organization name initials
 * when no logo URL is available.
 */
export function TenantLogo({ className, size = 32 }: TenantLogoProps) {
  const { organization } = useOrganization();

  if (!organization) {
    return (
      <div
        className={cn(
          'flex items-center justify-center rounded-md bg-accent text-accent-foreground font-semibold',
          className,
        )}
        style={{ width: size, height: size, fontSize: size * 0.4 }}
      >
        N
      </div>
    );
  }

  if (organization.logoUrl) {
    return (
      <Image
        src={organization.logoUrl}
        alt={organization.name}
        width={size}
        height={size}
        className={cn('rounded-md object-contain', className)}
      />
    );
  }

  const initials = organization.name
    .split(' ')
    .map((word) => word[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();

  return (
    <div
      className={cn(
        'flex items-center justify-center rounded-md bg-accent text-accent-foreground font-semibold',
        className,
      )}
      style={{ width: size, height: size, fontSize: size * 0.4 }}
    >
      {initials}
    </div>
  );
}
