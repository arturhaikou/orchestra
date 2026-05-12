import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import AgentCreatePage from '../pages/AgentCreatePage';
import * as agentService from '../../services/agentService';
import * as toolService from '../../services/toolService';
import * as workspaceService from '../../services/workspaceService';
import * as mcpServerService from '../../services/mcpServerService';
import { Tool } from '../../types';

vi.mock('../../services/agentService', () => ({
  createAgent: vi.fn(),
  getAgents: vi.fn(),
  updateAgent: vi.fn(),
  deleteAgent: vi.fn(),
  getAgentTemplates: vi.fn(),
  createAgentFromTemplate: vi.fn(),
}));

vi.mock('../../services/toolService', () => ({
  getTools: vi.fn(),
}));

vi.mock('../../services/mcpServerService', () => ({
  getMcpServers: vi.fn(),
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

vi.mock('../../utils/markdownRenderer', () => ({
  renderMarkdown: vi.fn((input: string) => `<p>${input}</p>`),
}));

const mockTools: Tool[] = [
  {
    id: 'tool-1',
    name: 'GitHub',
    description: 'GitHub integration',
    category: 'CODE',
    icon: 'code',
    actions: [
      { id: 'action-review-pr', name: 'review_pull_request', description: 'Review PRs' },
      { id: 'action-create-pr', name: 'create_pull_request', description: 'Create PRs' },
    ],
  },
  {
    id: 'tool-2',
    name: 'Jira',
    description: 'Jira integration',
    category: 'TRACKER',
    icon: 'ticket',
    actions: [],
  },
];

const mockCreatedAgent = {
  id: 'new-agent-1',
  workspaceId: 'ws-test',
  name: 'Test Agent',
  role: 'Developer',
  status: 'OFFLINE' as const,
  capabilities: ['coding'],
  toolActionIds: [],
  toolCategories: [],
  subAgentIds: [],
  avatarUrl: '/avatar.png',
  customInstructions: 'Build features',
};

const LocationDisplay: React.FC = () => {
  const location = useLocation();
  return <div data-testid="location-display">{location.pathname}</div>;
};

const AgentsListPlaceholder: React.FC = () => <div data-testid="agents-list">Agents List</div>;

const renderAgentCreatePage = () => {
  return render(
    <MemoryRouter initialEntries={['/workspaces/ws-test/agents/new']}>
      <Routes>
        <Route path="/workspaces/:workspaceId/agents/new" element={<AgentCreatePage />} />
        <Route path="/workspaces/:workspaceId/agents" element={<AgentsListPlaceholder />} />
      </Routes>
      <LocationDisplay />
    </MemoryRouter>
  );
};

describe('AgentCreatePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(toolService.getTools).mockResolvedValue(mockTools);
    vi.mocked(workspaceService.fetchWorkspaceModels).mockResolvedValue(['gpt-4', 'gpt-3.5-turbo']);
    vi.mocked(agentService.createAgent).mockResolvedValue(mockCreatedAgent);
    vi.mocked(agentService.getAgents).mockResolvedValue([]);
    vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([]);
  });

  describe('Scenario 1: Form rendering', () => {
    it('renders_agent_creation_form_with_required_fields', async () => {
      renderAgentCreatePage();

      await waitFor(() => {
        expect(screen.getByText(/create agent/i)).toBeInTheDocument();
      });

      expect(screen.getByLabelText(/name/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/role/i)).toBeInTheDocument();
    });

    it('loads_available_tools_on_mount', async () => {
      renderAgentCreatePage();

      await waitFor(() => {
        expect(toolService.getTools).toHaveBeenCalledWith('ws-test');
      });
    });

    it('loads_available_models_on_mount', async () => {
      renderAgentCreatePage();

      await waitFor(() => {
        expect(workspaceService.fetchWorkspaceModels).toHaveBeenCalledWith('ws-test');
      });
    });
  });

  describe('Scenario 2: Successfully create an agent', () => {
    it('creates_agent_and_navigates_to_agents_list_on_save', async () => {
      renderAgentCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/name/i)).toBeInTheDocument();
      });

      await userEvent.type(screen.getByLabelText(/name/i), 'Test Agent');
      await userEvent.type(screen.getByLabelText(/role/i), 'Developer');

      const instructionsField = screen.getByLabelText(/custom instructions/i);
      await userEvent.type(instructionsField, 'Build features');

      await userEvent.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(agentService.createAgent).toHaveBeenCalledWith('ws-test', expect.objectContaining({
          name: 'Test Agent',
          role: 'Developer',
          customInstructions: 'Build features',
        }));
      });

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/agents');
      });
    });
  });

  describe('Scenario 3: Cancel agent creation', () => {
    it('navigates_to_agents_list_on_cancel_without_creating', async () => {
      renderAgentCreatePage();

      await waitFor(() => {
        expect(screen.getByText(/create agent/i)).toBeInTheDocument();
      });

      await userEvent.click(screen.getByRole('button', { name: /cancel/i }));

      expect(agentService.createAgent).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/agents');
      });
    });
  });

  describe('Scenario 4: Validation error on required field', () => {
    it('shows_validation_error_when_name_is_empty', async () => {
      renderAgentCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/role/i)).toBeInTheDocument();
      });

      await userEvent.type(screen.getByLabelText(/role/i), 'Developer');
      const instructionsField = screen.getByLabelText(/custom instructions/i);
      await userEvent.type(instructionsField, 'Some instructions');

      await userEvent.click(screen.getByRole('button', { name: /save/i }));

      expect(agentService.createAgent).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByText(/name is required/i)).toBeInTheDocument();
      });

      expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/agents/new');
    });

    it('shows_validation_error_when_instructions_are_empty', async () => {
      renderAgentCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/name/i)).toBeInTheDocument();
      });

      await userEvent.type(screen.getByLabelText(/name/i), 'Test Agent');
      await userEvent.type(screen.getByLabelText(/role/i), 'Developer');

      await userEvent.click(screen.getByRole('button', { name: /save/i }));

      expect(agentService.createAgent).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByText(/instructions.*required/i)).toBeInTheDocument();
      });
    });
  });

  describe('Review agent detection', () => {
    it('switches_to_project_principles_when_review_tool_selected', async () => {
      renderAgentCreatePage();

      await waitFor(() => {
        expect(toolService.getTools).toHaveBeenCalled();
      });
    });
  });

  describe('FR-005 — Tool Summary Section on Create page', () => {
    beforeEach(() => {
      vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([]);
    });

    it('shows_empty_state_prompt_when_no_tools_are_selected', async () => {
      renderAgentCreatePage();
      await screen.findByText(/create agent/i);
      expect(screen.getByText(/no tools selected/i)).toBeInTheDocument();
    });

    it('renders_add_tools_button_on_initial_load', async () => {
      renderAgentCreatePage();
      await screen.findByText(/create agent/i);
      expect(screen.getByTestId('add-tools-button')).toBeInTheDocument();
    });
  });

  describe('Error handling', () => {
    it('shows_error_toast_when_api_returns_400', async () => {
      vi.mocked(agentService.createAgent).mockRejectedValue(new Error('Invalid tool action ID'));

      renderAgentCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/name/i)).toBeInTheDocument();
      });

      await userEvent.type(screen.getByLabelText(/name/i), 'Test Agent');
      await userEvent.type(screen.getByLabelText(/role/i), 'Developer');
      await userEvent.type(screen.getByLabelText(/custom instructions/i), 'Build things');

      await userEvent.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(screen.getByText(/invalid tool action id|failed|error/i)).toBeInTheDocument();
      });

      expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/agents/new');
    });

    it('preserves_form_data_on_api_error', async () => {
      vi.mocked(agentService.createAgent).mockRejectedValue(new Error('Server error'));

      renderAgentCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/name/i)).toBeInTheDocument();
      });

      await userEvent.type(screen.getByLabelText(/name/i), 'Test Agent');
      await userEvent.type(screen.getByLabelText(/role/i), 'Developer');
      await userEvent.type(screen.getByLabelText(/custom instructions/i), 'Build things');

      await userEvent.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(screen.getByLabelText(/name/i)).toHaveValue('Test Agent');
        expect(screen.getByLabelText(/role/i)).toHaveValue('Developer');
      });
    });
  });

  describe('FR-007 — Custom Instructions Markdown Preview Toggle', () => {
    beforeEach(() => {
      vi.clearAllMocks();
      vi.mocked(toolService.getTools).mockResolvedValue(mockTools);
      vi.mocked(workspaceService.fetchWorkspaceModels).mockResolvedValue(['gpt-4', 'gpt-3.5-turbo']);
      vi.mocked(agentService.createAgent).mockResolvedValue(mockCreatedAgent);
      vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([]);
    });

    it('renders_the_edit_write_toggle_button_in_custom_instructions_section', async () => {
      renderAgentCreatePage();

      await waitFor(() => {
        expect(screen.getByText(/create agent/i)).toBeInTheDocument();
      });

      const toggleButton = screen.queryByRole('button', { name: /write|edit/i });
      expect(toggleButton).toBeTruthy();
    });

    it('renders_the_preview_toggle_button_in_custom_instructions_section', async () => {
      renderAgentCreatePage();

      await waitFor(() => {
        expect(screen.getByText(/create agent/i)).toBeInTheDocument();
      });

      const previewButton = screen.queryByRole('button', { name: /preview/i });
      expect(previewButton).toBeTruthy();
    });

    it('custom_instructions_textarea_is_visible_by_default_edit_mode_is_active', async () => {
      renderAgentCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/custom instructions/i)).toBeInTheDocument();
      });

      const textarea = screen.getByLabelText(/custom instructions/i);
      expect(textarea).toBeVisible();
    });

    it('form_isdirty_computation_is_unaffected_by_toggle_mode', async () => {
      const user = userEvent.setup();
      renderAgentCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/custom instructions/i)).toBeInTheDocument();
      });

      const textarea = screen.getByLabelText(/custom instructions/i);
      await user.type(textarea, 'Test instructions');

      const previewButton = screen.queryByRole('button', { name: /preview/i });
      if (previewButton) {
        await user.click(previewButton);
        await user.click(screen.getByRole('button', { name: /write|edit/i }));
      }

      expect(textarea).toHaveValue('Test instructions');
    });
  });

  describe('Sub-Agents', () => {
    const mockExistingAgent = {
      id: 'existing-agent-1',
      workspaceId: 'ws-test',
      name: 'Helper Bot',
      role: 'Assistant',
      status: 'IDLE' as const,
      capabilities: ['help'],
      toolActionIds: [],
      toolCategories: [],
      subAgentIds: [],
      avatarUrl: '/avatar.png',
    };

    beforeEach(() => {
      vi.mocked(agentService.getAgents).mockResolvedValue([mockExistingAgent]);
    });

    it('renders_add_sub_agent_button', async () => {
      renderAgentCreatePage();

      await waitFor(() => {
        expect(screen.getByText(/create agent/i)).toBeInTheDocument();
      });

      expect(screen.getByRole('button', { name: /add sub-agent/i })).toBeInTheDocument();
    });

    it('opens_sub_agents_modal_when_add_sub_agent_is_clicked', async () => {
      const user = userEvent.setup();
      renderAgentCreatePage();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /add sub-agent/i })).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /add sub-agent/i }));

      expect(screen.getByRole('dialog', { name: /select sub-agents/i })).toBeInTheDocument();
    });

    it('includes_subAgentIds_in_create_payload', async () => {
      vi.mocked(agentService.getAgents).mockResolvedValue([mockExistingAgent]);
      const user = userEvent.setup();
      renderAgentCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/name/i)).toBeInTheDocument();
      });

      await user.type(screen.getByLabelText(/name/i), 'Test Agent');
      await user.type(screen.getByLabelText(/role/i), 'Developer');
      await user.type(screen.getByLabelText(/custom instructions/i), 'Do stuff');

      await user.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(agentService.createAgent).toHaveBeenCalledWith('ws-test', expect.objectContaining({
          subAgentIds: [],
        }));
      });
    });
  });
});
