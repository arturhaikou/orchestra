import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import AgentEditPage from '../pages/AgentEditPage';
import * as agentService from '../../services/agentService';
import * as toolService from '../../services/toolService';
import * as workspaceService from '../../services/workspaceService';
import { Agent, Tool } from '../../types';

vi.mock('../../services/agentService', () => ({
  getAgent: vi.fn(),
  getAgents: vi.fn(),
  createAgent: vi.fn(),
  updateAgent: vi.fn(),
  deleteAgent: vi.fn(),
  getAgentTemplates: vi.fn(),
  createAgentFromTemplate: vi.fn(),
}));

vi.mock('../../services/toolService', () => ({
  getTools: vi.fn(),
}));

vi.mock('../../services/workspaceService', () => ({
  fetchWorkspaceModels: vi.fn(),
  getWorkspaces: vi.fn(),
  deleteWorkspace: vi.fn(),
  createWorkspace: vi.fn(),
  createWorkspaceWithProvider: vi.fn(),
  updateWorkspace: vi.fn(),
  fetchDefaultModel: vi.fn(),
  fetchPlatformModels: vi.fn(),
  getWorkspaceProviderConfig: vi.fn(),
  updateWorkspaceProvider: vi.fn(),
}));

const mockAgent: Agent = {
  id: 'agent-1',
  workspaceId: 'ws-test',
  name: 'Support Bot',
  role: 'Customer Support',
  status: 'IDLE',
  capabilities: ['chat', 'email'],
  toolActionIds: ['action-1'],
  toolCategories: ['TRACKER'],
  avatarUrl: '/avatar.png',
  customInstructions: 'Help customers with their issues',
  projectPrinciples: '',
  model: null,
  templateId: null,
  templateVersion: null,
  isBuiltIn: false,
};

const mockBuiltInAgent: Agent = {
  id: 'agent-builtin',
  workspaceId: 'ws-test',
  name: 'Code Reviewer',
  role: 'Senior Developer',
  status: 'IDLE',
  capabilities: ['code-review'],
  toolActionIds: ['action-review'],
  toolCategories: ['CODE'],
  avatarUrl: '/avatar.png',
  customInstructions: '',
  projectPrinciples: 'Follow SOLID principles',
  model: 'gpt-4',
  templateId: 'code-review-template',
  templateVersion: 1,
  isBuiltIn: true,
};

const mockTools: Tool[] = [
  {
    id: 'tool-1',
    name: 'Jira',
    description: 'Jira integration',
    category: 'TRACKER',
    icon: 'ticket',
    actions: [{ id: 'action-1', name: 'read_tickets', description: 'Read tickets' }],
  },
];

const LocationDisplay: React.FC = () => {
  const location = useLocation();
  return <div data-testid="location-display">{location.pathname}</div>;
};

const AgentsListPlaceholder: React.FC = () => <div data-testid="agents-list">Agents List</div>;

const renderAgentEditPage = (agentId: string = 'agent-1') => {
  return render(
    <MemoryRouter initialEntries={[`/workspaces/ws-test/agents/${agentId}/edit`]}>
      <Routes>
        <Route path="/workspaces/:workspaceId/agents/:agentId/edit" element={<AgentEditPage />} />
        <Route path="/workspaces/:workspaceId/agents" element={<AgentsListPlaceholder />} />
      </Routes>
      <LocationDisplay />
    </MemoryRouter>
  );
};

describe('AgentEditPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(agentService.getAgent).mockResolvedValue(mockAgent);
    vi.mocked(agentService.updateAgent).mockResolvedValue({ ...mockAgent, role: 'Updated Role' });
    vi.mocked(toolService.getTools).mockResolvedValue(mockTools);
    vi.mocked(workspaceService.fetchWorkspaceModels).mockResolvedValue(['gpt-4', 'gpt-3.5-turbo']);
  });

  describe('Scenario 1: Navigate to agent edit page', () => {
    it('displays_agent_current_data_in_form_fields', async () => {
      renderAgentEditPage();

      await waitFor(() => {
        expect(screen.getByDisplayValue('Support Bot')).toBeInTheDocument();
      });
      expect(screen.getByDisplayValue('Customer Support')).toBeInTheDocument();
    });

    it('shows_loading_state_before_data_loads', () => {
      vi.mocked(agentService.getAgent).mockReturnValue(new Promise(() => {}));
      renderAgentEditPage();

      expect(screen.getByTestId('agent-edit-loading') ?? screen.getByRole('status')).toBeTruthy();
    });

    it('renders_page_title_with_edit_agent', async () => {
      renderAgentEditPage();

      await waitFor(() => {
        expect(screen.getByText('Edit Agent')).toBeInTheDocument();
      });
    });
  });

  describe('Scenario 2: Successfully update an agent', () => {
    it('updates_agent_and_navigates_to_agents_list', async () => {
      const user = userEvent.setup();
      renderAgentEditPage();

      await waitFor(() => {
        expect(screen.getByDisplayValue('Customer Support')).toBeInTheDocument();
      });

      const roleInput = screen.getByDisplayValue('Customer Support');
      await user.clear(roleInput);
      await user.type(roleInput, 'Updated Role');

      const saveButton = screen.getByRole('button', { name: /save/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(agentService.updateAgent).toHaveBeenCalledWith(
          'agent-1',
          expect.objectContaining({ role: 'Updated Role' })
        );
      });

      await waitFor(() => {
        expect(screen.getByTestId('location-display')).toHaveTextContent(
          '/workspaces/ws-test/agents'
        );
      });
    });

    it('shows_saving_spinner_during_submission', async () => {
      const user = userEvent.setup();
      vi.mocked(agentService.updateAgent).mockReturnValue(new Promise(() => {}));
      renderAgentEditPage();

      await waitFor(() => {
        expect(screen.getByDisplayValue('Customer Support')).toBeInTheDocument();
      });

      const saveButton = screen.getByRole('button', { name: /save/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/saving/i)).toBeInTheDocument();
      });
    });

    it('shows_error_toast_on_save_failure', async () => {
      const user = userEvent.setup();
      vi.mocked(agentService.updateAgent).mockRejectedValue(new Error('Validation failed'));
      renderAgentEditPage();

      await waitFor(() => {
        expect(screen.getByDisplayValue('Customer Support')).toBeInTheDocument();
      });

      const saveButton = screen.getByRole('button', { name: /save/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/validation failed|failed to update/i)).toBeInTheDocument();
      });
    });
  });

  describe('Scenario 3: Cancel editing without changes', () => {
    it('navigates_to_agents_list_on_cancel', async () => {
      const user = userEvent.setup();
      renderAgentEditPage();

      await waitFor(() => {
        expect(screen.getByDisplayValue('Support Bot')).toBeInTheDocument();
      });

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      await waitFor(() => {
        expect(screen.getByTestId('location-display')).toHaveTextContent(
          '/workspaces/ws-test/agents'
        );
      });
    });

    it('does_not_call_updateAgent_on_cancel', async () => {
      const user = userEvent.setup();
      renderAgentEditPage();

      await waitFor(() => {
        expect(screen.getByDisplayValue('Support Bot')).toBeInTheDocument();
      });

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      expect(agentService.updateAgent).not.toHaveBeenCalled();
    });
  });

  describe('Scenario 4: Agent not found', () => {
    it('displays_error_state_when_agent_not_found', async () => {
      vi.mocked(agentService.getAgent).mockRejectedValue(new Error('Agent not found'));
      renderAgentEditPage('nonexistent-id');

      await waitFor(() => {
        expect(screen.getByText(/agent not found/i)).toBeInTheDocument();
      });
    });

    it('provides_link_to_return_to_agents_list', async () => {
      vi.mocked(agentService.getAgent).mockRejectedValue(new Error('Agent not found'));
      renderAgentEditPage('nonexistent-id');

      await waitFor(() => {
        expect(screen.getByText(/return to agents/i)).toBeInTheDocument();
      });
    });

    it('navigates_to_agents_list_when_return_link_clicked', async () => {
      const user = userEvent.setup();
      vi.mocked(agentService.getAgent).mockRejectedValue(new Error('Agent not found'));
      renderAgentEditPage('nonexistent-id');

      await waitFor(() => {
        expect(screen.getByText(/return to agents/i)).toBeInTheDocument();
      });

      await user.click(screen.getByText(/return to agents/i));

      await waitFor(() => {
        expect(screen.getByTestId('location-display')).toHaveTextContent(
          '/workspaces/ws-test/agents'
        );
      });
    });
  });

  describe('Scenario 5: Built-in agent shows locked fields', () => {
    beforeEach(() => {
      vi.mocked(agentService.getAgent).mockResolvedValue(mockBuiltInAgent);
    });

    it('renders_name_as_read_only_for_built_in_agent', async () => {
      renderAgentEditPage('agent-builtin');

      await waitFor(() => {
        expect(screen.getByText('Code Reviewer')).toBeInTheDocument();
      });

      const nameInput = screen.queryByDisplayValue('Code Reviewer');
      if (nameInput) {
        expect(nameInput).toBeDisabled();
      }
    });

    it('renders_role_as_read_only_for_built_in_agent', async () => {
      renderAgentEditPage('agent-builtin');

      await waitFor(() => {
        expect(screen.getByText('Senior Developer')).toBeInTheDocument();
      });
    });

    it('allows_editing_project_principles_for_built_in_agent', async () => {
      const user = userEvent.setup();
      renderAgentEditPage('agent-builtin');

      await waitFor(() => {
        expect(screen.getByDisplayValue('Follow SOLID principles')).toBeInTheDocument();
      });

      const principlesInput = screen.getByDisplayValue('Follow SOLID principles');
      expect(principlesInput).not.toBeDisabled();
    });

    it('allows_editing_model_for_built_in_agent', async () => {
      renderAgentEditPage('agent-builtin');

      await waitFor(() => {
        const modelSelect = screen.getByLabelText(/model/i);
        expect(modelSelect).not.toBeDisabled();
      });
    });

    it('shows_info_banner_about_locked_fields', async () => {
      renderAgentEditPage('agent-builtin');

      await waitFor(() => {
        expect(screen.getByText(/locked|built-in|cannot be modified/i)).toBeInTheDocument();
      });
    });
  });
});
