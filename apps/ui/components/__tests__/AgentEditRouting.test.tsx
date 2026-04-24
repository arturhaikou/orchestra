import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import AgentEditPage from '../pages/AgentEditPage';
import * as agentService from '../../services/agentService';
import * as toolService from '../../services/toolService';
import * as workspaceService from '../../services/workspaceService';

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

const mockAgent = {
  id: 'agent-1',
  workspaceId: 'ws-test',
  name: 'Support Bot',
  role: 'Customer Support',
  status: 'IDLE' as const,
  capabilities: ['chat', 'email'],
  toolActionIds: ['action-1'],
  toolCategories: ['TRACKER'],
  avatarUrl: '/avatar.png',
  customInstructions: 'Help customers',
  projectPrinciples: '',
  model: null,
  templateId: null,
  templateVersion: null,
  isBuiltIn: false,
};

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

describe('AgentEditPage Routing', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(agentService.getAgent).mockResolvedValue(mockAgent);
    vi.mocked(toolService.getTools).mockResolvedValue([]);
    vi.mocked(workspaceService.fetchWorkspaceModels).mockResolvedValue(['gpt-4']);
  });

  it('mounts_AgentEditPage_at_correct_route', async () => {
    renderAgentEditPage();

    expect(screen.getByTestId('location-display')).toHaveTextContent(
      '/workspaces/ws-test/agents/agent-1/edit'
    );
  });

  it('loads_agent_data_on_mount', async () => {
    renderAgentEditPage();

    await waitFor(() => {
      expect(agentService.getAgent).toHaveBeenCalledWith('agent-1');
    });
  });

  it('displays_agent_name_after_loading', async () => {
    renderAgentEditPage();

    await waitFor(() => {
      expect(screen.getByDisplayValue('Support Bot')).toBeInTheDocument();
    });
  });
});
