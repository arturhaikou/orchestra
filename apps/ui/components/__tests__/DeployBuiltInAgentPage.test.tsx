import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import DeployBuiltInAgentPage from '../pages/DeployBuiltInAgentPage';
import * as agentService from '../../services/agentService';
import { AgentTemplateDto } from '../../types';

vi.mock('../../services/agentService', () => ({
  getAgent: vi.fn(),
  getAgents: vi.fn(),
  createAgent: vi.fn(),
  updateAgent: vi.fn(),
  deleteAgent: vi.fn(),
  getAgentTemplates: vi.fn(),
  createAgentFromTemplate: vi.fn(),
}));

const createMockTemplate = (overrides: Partial<AgentTemplateDto> = {}): AgentTemplateDto => ({
  templateId: 'code-review',
  name: 'Code Reviewer',
  role: 'Senior Developer',
  description: 'Analyzes pull requests for code quality, best practices, and potential bugs.',
  prerequisites: [
    { integrationType: 'CodeSource', providerName: 'GitHub', satisfied: true },
  ],
  availability: { status: 'AVAILABLE', reason: null, existingAgentId: null },
  capabilities: ['Pull Request Analysis', 'Code Quality Review'],
  toolLabel: 'Code Review Tools',
  usageGuide: 'Assign this agent to tickets requiring code review.',
  templateVersion: 1,
  ...overrides,
});

const mockAvailableTemplate = createMockTemplate();

const mockAlreadyDeployedTemplate = createMockTemplate({
  availability: {
    status: 'ALREADY_DEPLOYED',
    reason: null,
    existingAgentId: 'existing-agent-id',
  },
});

const mockUnavailableTemplate = createMockTemplate({
  prerequisites: [
    { integrationType: 'CodeSource', providerName: 'GitHub', satisfied: false },
  ],
  availability: {
    status: 'UNAVAILABLE',
    reason: 'Code Source integration required',
    existingAgentId: null,
  },
});

const mockDeployedAgent = {
  id: 'new-agent-1',
  workspaceId: 'ws-test',
  name: 'Code Reviewer',
  role: 'Senior Developer',
  status: 'OFFLINE' as const,
  capabilities: ['code-review'],
  toolActionIds: [],
  toolCategories: [],
  avatarUrl: '/avatar.png',
  customInstructions: '',
  projectPrinciples: 'Follow best practices',
  model: 'gpt-4',
  templateId: 'code-review',
  templateVersion: 1,
  isBuiltIn: true,
};

const LocationDisplay: React.FC = () => {
  const location = useLocation();
  return <div data-testid="location-display">{location.pathname}</div>;
};

const AgentsListPlaceholder: React.FC = () => <div data-testid="agents-list">Agents List</div>;

const renderDeployPage = (templateId: string = 'code-review') => {
  return render(
    <MemoryRouter initialEntries={[`/workspaces/ws-test/agents/deploy/${templateId}`]}>
      <Routes>
        <Route path="/workspaces/:workspaceId/agents/deploy/:templateId" element={<DeployBuiltInAgentPage />} />
        <Route path="/workspaces/:workspaceId/agents" element={<AgentsListPlaceholder />} />
      </Routes>
      <LocationDisplay />
    </MemoryRouter>
  );
};

describe('DeployBuiltInAgentPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Scenario 1: Navigate to deploy built-in agent page', () => {
    beforeEach(() => {
      vi.mocked(agentService.getAgentTemplates).mockResolvedValue([mockAvailableTemplate]);
    });

    it('shows_loading_state_while_fetching_template', () => {
      vi.mocked(agentService.getAgentTemplates).mockReturnValue(new Promise(() => {}));
      renderDeployPage();

      expect(screen.getByTestId('deploy-page-loading')).toBeInTheDocument();
    });

    it('displays_template_name_after_loading', async () => {
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByText('Code Reviewer')).toBeInTheDocument();
      });
    });

    it('displays_template_role', async () => {
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByText('Senior Developer')).toBeInTheDocument();
      });
    });

    it('displays_template_description', async () => {
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByText(/analyzes pull requests/i)).toBeInTheDocument();
      });
    });

    it('displays_template_capabilities', async () => {
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByText('Pull Request Analysis')).toBeInTheDocument();
        expect(screen.getByText('Code Quality Review')).toBeInTheDocument();
      });
    });

    it('displays_template_prerequisites_with_satisfied_status', async () => {
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByText(/GitHub/i)).toBeInTheDocument();
      });
    });

    it('displays_page_title', async () => {
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByText(/deploy built-in agent/i)).toBeInTheDocument();
      });
    });

    it('renders_deploy_button_enabled_when_available', async () => {
      renderDeployPage();

      await waitFor(() => {
        const deployButton = screen.getByRole('button', { name: /deploy/i });
        expect(deployButton).toBeEnabled();
      });
    });

    it('renders_cancel_button', async () => {
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
      });
    });
  });

  describe('Scenario 2: Successfully deploy a built-in agent', () => {
    beforeEach(() => {
      vi.mocked(agentService.getAgentTemplates).mockResolvedValue([mockAvailableTemplate]);
      vi.mocked(agentService.createAgentFromTemplate).mockResolvedValue(mockDeployedAgent);
    });

    it('deploys_agent_and_navigates_to_agents_list', async () => {
      const user = userEvent.setup();
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /deploy/i })).toBeEnabled();
      });

      await user.click(screen.getByRole('button', { name: /deploy/i }));

      await waitFor(() => {
        expect(agentService.createAgentFromTemplate).toHaveBeenCalledWith(
          expect.objectContaining({
            workspaceId: 'ws-test',
            templateId: 'code-review',
          })
        );
      });

      await waitFor(() => {
        expect(screen.getByTestId('location-display')).toHaveTextContent(
          '/workspaces/ws-test/agents'
        );
      });
    });

    it('shows_deploying_state_during_api_call', async () => {
      const user = userEvent.setup();
      vi.mocked(agentService.createAgentFromTemplate).mockReturnValue(new Promise(() => {}));
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /deploy/i })).toBeEnabled();
      });

      await user.click(screen.getByRole('button', { name: /deploy/i }));

      await waitFor(() => {
        expect(screen.getByText(/deploying/i)).toBeInTheDocument();
      });
    });

    it('disables_buttons_during_deploy', async () => {
      const user = userEvent.setup();
      vi.mocked(agentService.createAgentFromTemplate).mockReturnValue(new Promise(() => {}));
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /deploy/i })).toBeEnabled();
      });

      await user.click(screen.getByRole('button', { name: /deploy/i }));

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /deploying/i })).toBeDisabled();
      });
    });

    it('shows_error_toast_on_deploy_failure_and_stays_on_page', async () => {
      const user = userEvent.setup();
      vi.mocked(agentService.createAgentFromTemplate).mockRejectedValue(new Error('Integration required'));
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /deploy/i })).toBeEnabled();
      });

      await user.click(screen.getByRole('button', { name: /deploy/i }));

      await waitFor(() => {
        expect(screen.getByText(/integration required|failed|error/i)).toBeInTheDocument();
      });

      expect(screen.getByTestId('location-display')).toHaveTextContent(
        '/workspaces/ws-test/agents/deploy/code-review'
      );
    });

    it('re_enables_deploy_button_after_failure', async () => {
      const user = userEvent.setup();
      vi.mocked(agentService.createAgentFromTemplate).mockRejectedValue(new Error('Server error'));
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /deploy/i })).toBeEnabled();
      });

      await user.click(screen.getByRole('button', { name: /deploy/i }));

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /deploy/i })).toBeEnabled();
      });
    });
  });

  describe('Scenario 3: Cancel deployment', () => {
    beforeEach(() => {
      vi.mocked(agentService.getAgentTemplates).mockResolvedValue([mockAvailableTemplate]);
    });

    it('navigates_to_agents_list_on_cancel_without_deploying', async () => {
      const user = userEvent.setup();
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /cancel/i }));

      expect(agentService.createAgentFromTemplate).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByTestId('location-display')).toHaveTextContent(
          '/workspaces/ws-test/agents'
        );
      });
    });
  });

  describe('Scenario 4: Template already deployed', () => {
    beforeEach(() => {
      vi.mocked(agentService.getAgentTemplates).mockResolvedValue([mockAlreadyDeployedTemplate]);
    });

    it('shows_already_deployed_warning_banner', async () => {
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByText(/already deployed|already active/i)).toBeInTheDocument();
      });
    });

    it('disables_deploy_button_when_already_deployed', async () => {
      renderDeployPage();

      await waitFor(() => {
        const deployButton = screen.queryByRole('button', { name: /deploy/i });
        if (deployButton) {
          expect(deployButton).toBeDisabled();
        }
      });
    });

    it('shows_deployed_status_badge', async () => {
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByText(/deployed/i)).toBeInTheDocument();
      });
    });
  });

  describe('Scenario 5: Unmet prerequisites', () => {
    beforeEach(() => {
      vi.mocked(agentService.getAgentTemplates).mockResolvedValue([mockUnavailableTemplate]);
    });

    it('shows_missing_prerequisites_error_banner', async () => {
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByText(/missing|required|unavailable/i)).toBeInTheDocument();
      });
    });

    it('lists_unsatisfied_prerequisites', async () => {
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByText(/GitHub/i)).toBeInTheDocument();
      });
    });

    it('disables_deploy_button_when_prerequisites_unmet', async () => {
      renderDeployPage();

      await waitFor(() => {
        const deployButton = screen.queryByRole('button', { name: /deploy/i });
        if (deployButton) {
          expect(deployButton).toBeDisabled();
        }
      });
    });

    it('shows_blocked_status_badge', async () => {
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByText(/blocked/i)).toBeInTheDocument();
      });
    });
  });

  describe('Template not found', () => {
    beforeEach(() => {
      vi.mocked(agentService.getAgentTemplates).mockResolvedValue([mockAvailableTemplate]);
    });

    it('shows_not_found_state_when_templateId_does_not_match', async () => {
      renderDeployPage('nonexistent-template');

      await waitFor(() => {
        expect(screen.getByText(/template not found/i)).toBeInTheDocument();
      });
    });

    it('shows_back_link_when_template_not_found', async () => {
      renderDeployPage('nonexistent-template');

      await waitFor(() => {
        expect(screen.getByText(/back to agents|return to agents/i)).toBeInTheDocument();
      });
    });
  });

  describe('API fetch failure', () => {
    it('shows_error_state_when_template_fetch_fails', async () => {
      vi.mocked(agentService.getAgentTemplates).mockRejectedValue(new Error('Network error'));
      renderDeployPage();

      await waitFor(() => {
        expect(screen.getByText(/error|failed to load/i)).toBeInTheDocument();
      });
    });
  });
});
