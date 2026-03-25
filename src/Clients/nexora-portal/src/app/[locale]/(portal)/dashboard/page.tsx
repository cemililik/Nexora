import { DashboardContent } from './DashboardContent';

/**
 * Portal dashboard — server component wrapper.
 * User data is provided by the parent layout via PortalShell (Zustand).
 */
export default function DashboardPage() {
  return <DashboardContent />;
}
