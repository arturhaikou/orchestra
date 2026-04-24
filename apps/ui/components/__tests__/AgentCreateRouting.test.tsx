import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';
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
  onConnectionStatusChange: vi.fn(),
  getConnectionStatus: vi.fn().mockReturnValue('connected'),
}));

const mockWorkspaces = [
  { id: 'ws-a', name: 'Workspace A', isAiSummarizationEnabled: false, isCustomerSatisfactionAnalysisEnabled: false, ownerId: 'user-1' },
];

const mockUser = { id: 'user-1', name: 'Test User', email: 'test@example.com' };

const LocationDisplay: React.FC = () => {
  const location = useLocation();
  return <div data-testid="location-display">{location.pathname}</div>;
};

const renderApp = (initialEntry: string) => {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <App />
      <LocationDisplay />
    </MemoryRouter>
  );
};

describe('FR-002: Agent Create Page Routing', () => {
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

    vi.mocked(authService.getToken).mockReturnValue('valid-token');
    vi.mocked(authService.getUser).mockReturnValue(mockUser);
    vi.mocked(workspaceService.getWorkspaces).mockResolvedValue(mockWorkspaces);
    vi.mocked(workspaceService.fetchWorkspaceModels).mockResolvedValue([]);
    vi.mocked(workspaceService.fetchDefaultModel).mockResolvedValue(null as any);
    vi.mocked(workspaceService.fetchPlatformModels).mockResolvedValue([]);
  });

  describe('Scenario 1: Navigate to agent creation page', () => {
    it('navigates_to_agents_new_when_clicking_create_agent', async () => {
      renderApp('/workspaces/ws-a/agents');

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-a/agents');
      });

      const deployButton = await screen.findByRole('button', { name: /deploy agent|create agent/i });
      await userEvent.click(deployButton);

      const createFromScratch = await screen.findByText(/create from scratch/i);
      await userEvent.click(createFromScratch);

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-a/agents/new');
      });
    });
  });

  describe('Scenario 3: Cancel agent creation', () => {
    it('navigates_back_to_agents_list_on_cancel', async () => {
      renderApp('/workspaces/ws-a/agents/new');

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-a/agents/new');
      });

      const cancelButton = await screen.findByRole('button', { name: /cancel/i });
      await userEvent.click(cancelButton);

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-a/agents');
      });
    });
  });

  describe('Scenario 5: Direct URL access to agent creation page', () => {
    it('renders_agent_creation_form_on_direct_url_access', async () => {
      renderApp('/workspaces/ws-a/agents/new');

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-a/agents/new');
      });

      await waitFor(() => {
        expect(screen.getByText(/create agent/i)).toBeInTheDocument();
      });
    });

    it('redirects_to_login_when_unauthenticated', async () => {
      vi.mocked(authService.getToken).mockReturnValue(null);

      renderApp('/workspaces/ws-a/agents/new');

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/login');
      });
    });
  });
});
