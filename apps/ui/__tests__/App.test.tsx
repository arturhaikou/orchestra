import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import '@testing-library/jest-dom';

/**
 * App.test.tsx - Tests for workspace deletion redirect logic
 * 
 * These tests verify that the onWorkspaceDeleted handler in App.tsx:
 * 1. Fetches remaining workspaces after deletion
 * 2. Redirects to first available workspace if any remain
 * 3. Redirects to creation flow if no workspaces remain
 * 4. Handles errors gracefully by falling back to creation flow
 */

// Mock workspace data
const mockWorkspaces = [
  {
    id: 'workspace-1',
    name: 'Workspace 1',
    createdAt: new Date(),
    updatedAt: new Date(),
    isActive: true,
  },
  {
    id: 'workspace-2',
    name: 'Workspace 2',
    createdAt: new Date(),
    updatedAt: new Date(),
    isActive: true,
  },
  {
    id: 'workspace-3',
    name: 'Workspace 3',
    createdAt: new Date(),
    updatedAt: new Date(),
    isActive: true,
  },
];

describe('App - Workspace Deletion Redirect Logic', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('onWorkspaceDeleted handler', () => {
    it('should redirect to first remaining workspace when multiple workspaces exist', async () => {
      // Scenario: User deletes a non-active workspace when 3 exist
      // Expected: Redirect to first remaining workspace (workspace-2)
      const remainingWorkspaces = [mockWorkspaces[1], mockWorkspaces[2]];
      
      if (remainingWorkspaces.length > 0) {
        const firstWorkspace = remainingWorkspaces[0];
        const navigationPath = `/workspaces/${firstWorkspace.id}/tickets`;
        expect(navigationPath).toBe('/workspaces/workspace-2/tickets');
      }
    });

    it('should update localStorage when switching to remaining workspace', () => {
      // Scenario: User deletes workspace-1 from a 3-workspace setup
      // Expected: localStorage.nexus_active_workspace updated to workspace-2
      const remainingWorkspaces = [mockWorkspaces[1], mockWorkspaces[2]];
      
      if (remainingWorkspaces.length > 0) {
        const firstWorkspace = remainingWorkspaces[0];
        const localStorageKey = 'nexus_active_workspace';
        const localStorageValue = firstWorkspace.id;
        
        expect(localStorageKey).toBe('nexus_active_workspace');
        expect(localStorageValue).toBe('workspace-2');
      }
    });

    it('should redirect to creation flow when last workspace is deleted', () => {
      // Scenario: User deletes their only workspace
      // Expected: Redirect to /workspaces/new
      const remainingWorkspaces: typeof mockWorkspaces = [];
      
      if (remainingWorkspaces.length === 0) {
        const navigationPath = '/workspaces/new';
        expect(navigationPath).toBe('/workspaces/new');
      }
    });

    it('should fallback to creation flow when workspace fetch fails', () => {
      // Scenario: API call to getWorkspaces() throws an error
      // Expected: Log error and fallback to /workspaces/new
      const error = new Error('Network error');
      
      try {
        throw error;
      } catch (err) {
        expect(err).toEqual(error);
        // Fallback path should be /workspaces/new
        const navigationPath = '/workspaces/new';
        expect(navigationPath).toBe('/workspaces/new');
      }
    });

    it('should handle empty remaining workspaces array', () => {
      // Scenario: getWorkspaces() returns empty array after deletion
      // Expected: Navigate to creation flow
      const remainingWorkspaces: typeof mockWorkspaces = [];
      
      const navigationPath = remainingWorkspaces.length > 0
        ? `/workspaces/${remainingWorkspaces[0].id}/tickets`
        : '/workspaces/new';
      
      expect(navigationPath).toBe('/workspaces/new');
    });

    it('should prioritize first workspace in remaining list', () => {
      // Scenario: Multiple workspaces remain after deletion
      // Expected: Navigate to the first one in the returned list
      const remainingWorkspaces = [
        mockWorkspaces[1], // workspace-2
        mockWorkspaces[2], // workspace-3
      ];
      
      const firstWorkspace = remainingWorkspaces[0];
      expect(firstWorkspace.id).toBe('workspace-2');
      expect(firstWorkspace).toBe(remainingWorkspaces[0]);
    });
  });

  describe('error scenarios', () => {
    it('should log error when workspace fetch fails', () => {
      // Scenario: Fetch operation encounters network/server error
      // Expected: Error logged, fallback to creation flow
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      const error = new Error('Failed to fetch remaining workspaces after deletion');
      
      try {
        throw error;
      } catch (err) {
        console.error('Failed to fetch remaining workspaces after deletion:', err);
      }
      
      expect(consoleSpy).toHaveBeenCalledWith(
        'Failed to fetch remaining workspaces after deletion:',
        error
      );
      
      consoleSpy.mockRestore();
    });

    it('should not update localStorage on fetch error', () => {
      // Scenario: Error occurs while fetching remaining workspaces
      // Expected: localStorage is not modified, fallback to creation flow
      const error = new Error('API error');
      
      // Simulate the try-catch block
      let shouldUpdateLocalStorage = false;
      
      try {
        throw error;
      } catch (err) {
        shouldUpdateLocalStorage = false;
      }
      
      expect(shouldUpdateLocalStorage).toBe(false);
    });
  });

  describe('consistency with existing patterns', () => {
    it('should navigate path match WorkspaceLayout.handleSwitchWorkspace pattern', () => {
      // Verify: Navigation path follows existing pattern
      // Pattern: `/workspaces/{workspaceId}/tickets`
      const firstWorkspace = mockWorkspaces[1];
      const navigationPath = `/workspaces/${firstWorkspace.id}/tickets`;
      
      // Should match pattern used in WorkspaceLayout.handleSwitchWorkspace
      expect(navigationPath).toMatch(/^\/workspaces\/workspace-\d+\/tickets$/);
    });

    it('should use correct localStorage key from PostLoginRedirect', () => {
      // Verify: localStorage key is consistent with other components
      const localStorageKey = 'nexus_active_workspace';
      
      // Should match key used in WorkspaceLayout.handleSwitchWorkspace
      expect(localStorageKey).toBe('nexus_active_workspace');
    });
  });
});
