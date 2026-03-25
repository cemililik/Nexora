import { ProfileContent } from './ProfileContent';

/**
 * User profile page — server component wrapper.
 * User data is provided by the parent layout via PortalShell (Zustand).
 */
export default function ProfilePage() {
  return <ProfileContent />;
}
