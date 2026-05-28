import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
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

const mockTemplate: AgentTemplateDto = {
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
  isCliAgent: false,
  editableFields: [],
  availableOptionalTools: [],
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

describe('DeployBuiltInAgentPage Routing', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(agentService.getAgentTemplates).mockResolvedValue([mockTemplate]);
  });

  it('mounts_DeployBuiltInAgentPage_at_correct_route', () => {
    renderDeployPage();

    expect(screen.getByTestId('location-display')).toHaveTextContent(
      '/workspaces/ws-test/agents/deploy/code-review'
    );
  });

  it('calls_getAgentTemplates_on_mount_with_workspaceId', async () => {
    renderDeployPage();

    await waitFor(() => {
      expect(agentService.getAgentTemplates).toHaveBeenCalledWith('ws-test');
    });
  });

  it('displays_template_name_after_loading', async () => {
    renderDeployPage();

    await waitFor(() => {
      expect(screen.getByText('Code Reviewer')).toBeInTheDocument();
    });
  });
});
