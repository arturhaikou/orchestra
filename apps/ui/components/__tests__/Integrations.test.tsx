import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import Integrations from '../Integrations';
import * as integrationService from '../../services/integrationService';
import { IntegrationType } from '../../types';

vi.mock('../../services/integrationService', () => ({
  getIntegrations: vi.fn(),
  deleteIntegration: vi.fn(),
  syncIntegrationTools: vi.fn(),
  getDeletionImpact: vi.fn(),
}));

const mcpServerRecord = {
  id: 'mcp-001',
  workspaceId: 'ws-test',
  name: 'My MCP Server',
  types: ['MCP_SERVER' as any],
  provider: 'mcp',
  icon: 'mcp',
  connected: true,
  lastSync: '2026-01-01',
  url: 'http://localhost:3000',
};

const trackerRecord = {
  id: 'int-001',
  workspaceId: 'ws-test',
  name: 'My Jira',
  types: [IntegrationType.TRACKER],
  provider: 'jira',
  icon: 'jira',
  connected: true,
  lastSync: '2026-01-01',
  url: 'https://test.atlassian.net',
};

const knowledgeBaseRecord = {
  id: 'int-002',
  workspaceId: 'ws-test',
  name: 'My Confluence',
  types: [IntegrationType.KNOWLEDGE_BASE],
  provider: 'confluence',
  icon: 'confluence',
  connected: true,
  lastSync: '2026-01-01',
  url: 'https://test.atlassian.net/wiki',
};

const renderIntegrations = () =>
  render(
    <MemoryRouter initialEntries={['/workspaces/ws-test/integrations']}>
      <Routes>
        <Route path="/workspaces/:workspaceId/integrations" element={<Integrations />} />
        <Route path="/workspaces/:workspaceId/integrations/new" element={<div>Create Page</div>} />
      </Routes>
    </MemoryRouter>
  );

beforeEach(() => {
  vi.clearAllMocks();
});

describe('Scenario 1: Integrations list does not show MCP Server category or cards', () => {
  beforeEach(() => {
    vi.mocked(integrationService.getIntegrations).mockResolvedValue([
      mcpServerRecord,
      trackerRecord,
    ]);
  });

  it('does_not_render_mcp_servers_section_header', async () => {
    renderIntegrations();

    await waitFor(() => {
      expect(screen.getByText('My Jira')).toBeInTheDocument();
    });
    expect(screen.queryByText('MCP Servers')).not.toBeInTheDocument();
  });

  it('does_not_render_mcp_server_card_by_name', async () => {
    renderIntegrations();

    await waitFor(() => {
      expect(screen.getByText('My Jira')).toBeInTheDocument();
    });
    expect(screen.queryByText('My MCP Server')).not.toBeInTheDocument();
  });

  it('renders_non_mcp_integrations_when_mcp_record_exists_in_response', async () => {
    renderIntegrations();

    await waitFor(() => {
      expect(screen.getByText('My Jira')).toBeInTheDocument();
    });
  });
});

describe('Scenario 2: Add MCP Server button is not present', () => {
  beforeEach(() => {
    vi.mocked(integrationService.getIntegrations).mockResolvedValue([]);
  });

  it('does_not_render_add_mcp_server_button', async () => {
    renderIntegrations();

    await waitFor(() => {
      expect(screen.queryByRole('button', { name: /add mcp server/i })).not.toBeInTheDocument();
    });
  });

  it('renders_add_connection_button', async () => {
    renderIntegrations();

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /add connection/i })).toBeInTheDocument();
    });
  });
});

describe('Scenario 3: Non-MCP integrations are unaffected', () => {
  beforeEach(() => {
    vi.mocked(integrationService.getIntegrations).mockResolvedValue([
      trackerRecord,
      knowledgeBaseRecord,
    ]);
  });

  it('renders_tracker_section_with_jira_card', async () => {
    renderIntegrations();

    await waitFor(() => {
      expect(screen.getByText('Tracker Systems')).toBeInTheDocument();
      expect(screen.getByText('My Jira')).toBeInTheDocument();
    });
  });

  it('renders_knowledge_base_section_with_confluence_card', async () => {
    renderIntegrations();

    await waitFor(() => {
      expect(screen.getByText('Knowledge Bases')).toBeInTheDocument();
      expect(screen.getByText('My Confluence')).toBeInTheDocument();
    });
  });

  it('renders_configure_link_for_non_mcp_integration', async () => {
    renderIntegrations();

    await waitFor(() => {
      expect(screen.getAllByRole('link', { name: /configure/i }).length).toBeGreaterThan(0);
    });
  });

  it('renders_delete_button_for_non_mcp_integration', async () => {
    renderIntegrations();

    await waitFor(() => {
      expect(screen.getAllByTitle(/delete/i).length).toBeGreaterThan(0);
    });
  });
});

describe('Edge case: Empty state without MCP category', () => {
  beforeEach(() => {
    vi.mocked(integrationService.getIntegrations).mockResolvedValue([]);
  });

  it('renders_empty_state_for_tracker_systems', async () => {
    renderIntegrations();

    await waitFor(() => {
      expect(screen.getByText(/no active tracker systems registered/i)).toBeInTheDocument();
    });
  });

  it('does_not_render_empty_state_for_mcp_servers', async () => {
    renderIntegrations();

    await waitFor(() => {
      expect(screen.getByText(/no active tracker systems registered/i)).toBeInTheDocument();
    });
    expect(screen.queryByText(/no active mcp servers registered/i)).not.toBeInTheDocument();
  });
});
