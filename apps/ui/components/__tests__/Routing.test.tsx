import React from 'react';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route, useLocation, useNavigate, Link, Navigate, Outlet } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import * as authService from '../../services/authService';
import * as workspaceService from '../../services/workspaceService';
import * as signalRService from '../../services/signalRService';
import App from '../../App';

vi.mock('../../services/authService', () => ({
  getToken: vi.fn(),
  getUser: vi.fn(),
  login: vi.fn(),
  logout: vi.fn(),
  register: vi.fn(),
  updateUser: vi.fn(),
  changePassword: vi.fn(),
}));

vi.mock('../../services/workspaceService', () => ({
  getWorkspaces: vi.fn(),
  deleteWorkspace: vi.fn(),
  createWorkspace: vi.fn(),
  createWorkspaceWithProvider: vi.fn(),
  updateWorkspace: vi.fn(),
  fetchWorkspaceModels: vi.fn(),
  fetchDefaultModel: vi.fn(),
  fetchPlatformModels: vi.fn(),
  getWorkspaceProviderConfig: vi.fn(),
  updateWorkspaceProvider: vi.fn(),
}));

vi.mock('../../services/signalRService', () => ({
  connect: vi.fn().mockResolvedValue(undefined),
  disconnect: vi.fn().mockResolvedValue(undefined),
  switchWorkspace: vi.fn().mockResolvedValue(undefined),
  onModelPullProgress: vi.fn(),
  onModelPullCompleted: vi.fn(),
  onModelPullFailed: vi.fn(),
  onAgentExecutionCompleted: vi.fn(),
  offAgentExecutionCompleted: vi.fn(),
  onTicketStatusChanged: vi.fn(),
  offTicketStatusChanged: vi.fn(),
  onReconnected: vi.fn(),
  offReconnected: vi.fn(),
  onConnectionStatusChange: vi.fn(),
  getConnectionStatus: vi.fn().mockReturnValue('connected'),
}));

const mockWorkspaces = [
  { id: 'ws-a', name: 'Workspace A', isAiSummarizationEnabled: false, isCustomerSatisfactionAnalysisEnabled: false, ownerId: 'user-1' },
  { id: 'ws-b', name: 'Workspace B', isAiSummarizationEnabled: false, isCustomerSatisfactionAnalysisEnabled: false, ownerId: 'user-1' },
];

const mockUser = { id: 'user-1', name: 'Test User', email: 'test@example.com' };

const LocationDisplay: React.FC = () => {
  const location = useLocation();
  const navigate = useNavigate();
  return (
    <>
      <div data-testid="location-display">{location.pathname}</div>
      <button data-testid="go-back" onClick={() => navigate(-1)}>Back</button>
    </>
  );
};

const renderApp = (initialEntry: string = '/') => {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <App />
      <LocationDisplay />
    </MemoryRouter>
  );
};

describe('Route-Based Navigation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn().mockImplementation((query: string) => ({
        matches: query === '(prefers-color-scheme: dark)',
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      })),
    });
  });

  describe('Scenario 1: Sidebar navigation', () => {
    it('navigates_to_agents_when_clicking_agents_sidebar_link', async () => {
      vi.mocked(authService.getToken).mockReturnValue('valid-token');
      vi.mocked(authService.getUser).mockReturnValue(mockUser);
      vi.mocked(workspaceService.getWorkspaces).mockResolvedValue(mockWorkspaces);

      renderApp('/workspaces/ws-a/tickets');

      await waitFor(() => {
        expect(screen.getByText('Agents')).toBeInTheDocument();
      });

      await userEvent.click(screen.getByText('Agents'));

      await waitFor(() => {
        const locationEl = screen.getByTestId('location-display');
        expect(locationEl.textContent).toBe('/workspaces/ws-a/agents');
      });
    });
  });

  describe('Scenario 2: Browser back button', () => {
    it('navigates_back_to_tickets_after_going_to_agents', async () => {
      vi.mocked(authService.getToken).mockReturnValue('valid-token');
      vi.mocked(authService.getUser).mockReturnValue(mockUser);
      vi.mocked(workspaceService.getWorkspaces).mockResolvedValue(mockWorkspaces);

      const { container } = renderApp('/workspaces/ws-a/tickets');

      await waitFor(() => {
        expect(screen.getByText('Agents')).toBeInTheDocument();
      });
      await userEvent.click(screen.getByText('Agents'));

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-a/agents');
      });

      await userEvent.click(screen.getByTestId('go-back'));

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-a/tickets');
      });
    });
  });

  describe('Scenario 3: Direct URL access', () => {
    it('displays_integrations_when_navigating_directly', async () => {
      vi.mocked(authService.getToken).mockReturnValue('valid-token');
      vi.mocked(authService.getUser).mockReturnValue(mockUser);
      vi.mocked(workspaceService.getWorkspaces).mockResolvedValue(mockWorkspaces);

      renderApp('/workspaces/ws-a/integrations');

      await waitFor(() => {
        const locationEl = screen.getByTestId('location-display');
        expect(locationEl.textContent).toBe('/workspaces/ws-a/integrations');
      });
    });
  });

  describe('Scenario 4: Post-login redirect', () => {
    it('redirects_to_workspace_tickets_after_login', async () => {
      vi.mocked(authService.getToken).mockReturnValue('valid-token');
      vi.mocked(authService.getUser).mockReturnValue(mockUser);
      vi.mocked(workspaceService.getWorkspaces).mockResolvedValue(mockWorkspaces);

      renderApp('/');

      await waitFor(() => {
        const locationEl = screen.getByTestId('location-display');
        expect(locationEl.textContent).toBe('/workspaces/ws-a/tickets');
      });
    });
  });

  describe('Scenario 5: Unknown route redirect', () => {
    it('redirects_nonexistent_route_to_tickets', async () => {
      vi.mocked(authService.getToken).mockReturnValue('valid-token');
      vi.mocked(authService.getUser).mockReturnValue(mockUser);
      vi.mocked(workspaceService.getWorkspaces).mockResolvedValue(mockWorkspaces);

      renderApp('/workspaces/ws-a/nonexistent');

      await waitFor(() => {
        const locationEl = screen.getByTestId('location-display');
        expect(locationEl.textContent).toBe('/workspaces/ws-a/tickets');
      });
    });
  });

  describe('Scenario 6: Workspace switching', () => {
    it('navigates_to_new_workspace_tickets_on_switch', async () => {
      vi.mocked(authService.getToken).mockReturnValue('valid-token');
      vi.mocked(authService.getUser).mockReturnValue(mockUser);
      vi.mocked(workspaceService.getWorkspaces).mockResolvedValue(mockWorkspaces);

      renderApp('/workspaces/ws-a/agents');

      await waitFor(() => {
        expect(screen.getByText('Workspace A')).toBeInTheDocument();
      });

      await userEvent.click(screen.getByText('Workspace A'));

      await waitFor(() => {
        expect(screen.getByText('Workspace B')).toBeInTheDocument();
      });
      await userEvent.click(screen.getByText('Workspace B'));

      await waitFor(() => {
        const locationEl = screen.getByTestId('location-display');
        expect(locationEl.textContent).toBe('/workspaces/ws-b/tickets');
      });
    });
  });

  describe('Scenario 7: Auth guard', () => {
    it('redirects_to_login_when_not_authenticated', async () => {
      vi.mocked(authService.getToken).mockReturnValue(null);

      renderApp('/workspaces/ws-a/agents');

      await waitFor(() => {
        const locationEl = screen.getByTestId('location-display');
        expect(locationEl.textContent).toBe('/login');
      });
    });
  });

  describe('Scenario 8: localStorage ignored', () => {
    it('ignores_nexus_active_view_localStorage_and_defaults_to_tickets', async () => {
      localStorage.setItem('nexus_active_view', 'integrations');
      vi.mocked(authService.getToken).mockReturnValue('valid-token');
      vi.mocked(authService.getUser).mockReturnValue(mockUser);
      vi.mocked(workspaceService.getWorkspaces).mockResolvedValue(mockWorkspaces);

      renderApp('/');

      await waitFor(() => {
        const locationEl = screen.getByTestId('location-display');
        expect(locationEl.textContent).toBe('/workspaces/ws-a/tickets');
      });

      expect(screen.getByTestId('location-display').textContent).not.toContain('integrations');
    });
  });

  describe('Access Denied: Non-member workspace', () => {
    it('shows_access_denied_for_non_member_workspace', async () => {
      vi.mocked(authService.getToken).mockReturnValue('valid-token');
      vi.mocked(authService.getUser).mockReturnValue(mockUser);
      vi.mocked(workspaceService.getWorkspaces).mockResolvedValue(mockWorkspaces);

      renderApp('/workspaces/foreign-id/tickets');

      await waitFor(() => {
        expect(screen.getByText('Access Denied')).toBeInTheDocument();
      });

      expect(screen.getByText(/not a member/i)).toBeInTheDocument();
    });
  });
});
