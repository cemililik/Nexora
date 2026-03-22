import { beforeEach, describe, expect, it } from 'vitest';

import { useUiStore } from './uiStore';

describe('uiStore', () => {
  beforeEach(() => {
    useUiStore.setState({
      sidebarOpen: true,
      theme: 'system',
      breadcrumbs: [],
    });
  });

  it('should have correct initial state', () => {
    const state = useUiStore.getState();
    expect(state.sidebarOpen).toBe(true);
    expect(state.theme).toBe('system');
    expect(state.breadcrumbs).toEqual([]);
  });

  it('should toggle sidebar', () => {
    useUiStore.getState().toggleSidebar();
    expect(useUiStore.getState().sidebarOpen).toBe(false);

    useUiStore.getState().toggleSidebar();
    expect(useUiStore.getState().sidebarOpen).toBe(true);
  });

  it('should set sidebar open state directly', () => {
    useUiStore.getState().setSidebarOpen(false);
    expect(useUiStore.getState().sidebarOpen).toBe(false);
  });

  it('should set theme', () => {
    useUiStore.getState().setTheme('dark');
    expect(useUiStore.getState().theme).toBe('dark');

    useUiStore.getState().setTheme('light');
    expect(useUiStore.getState().theme).toBe('light');
  });

  it('should set breadcrumbs', () => {
    const crumbs = [
      { label: 'lockey_nav_dashboard', path: '/dashboard' },
      { label: 'lockey_nav_users' },
    ];
    useUiStore.getState().setBreadcrumbs(crumbs);
    expect(useUiStore.getState().breadcrumbs).toEqual(crumbs);
  });
});
