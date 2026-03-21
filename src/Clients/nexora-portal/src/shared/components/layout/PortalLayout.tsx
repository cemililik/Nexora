'use client';

import { type ReactNode } from 'react';

import { cn } from '@/shared/lib/utils';
import { useUiStore } from '@/shared/lib/stores/uiStore';

import { Footer } from './Footer';
import { Sidebar } from './Sidebar';
import { Topbar } from './Topbar';

interface PortalLayoutProps {
  children: ReactNode;
}

/**
 * Main portal shell layout with sidebar, topbar, content area, and footer.
 * The sidebar width is managed by the UI store.
 */
export function PortalLayout({ children }: PortalLayoutProps) {
  const sidebarOpen = useUiStore((s) => s.sidebarOpen);

  return (
    <div className="min-h-screen bg-background">
      <Sidebar />
      <Topbar />
      <main
        className={cn(
          'min-h-[calc(100vh-8rem)] p-6 pt-20 transition-all duration-300',
          sidebarOpen ? 'ml-64' : 'ml-16',
        )}
      >
        {children}
      </main>
      <Footer />
    </div>
  );
}
