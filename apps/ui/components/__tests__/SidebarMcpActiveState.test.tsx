import React from 'react';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import Sidebar from '../Sidebar';
import * as authService from '../../services/authService';

vi.mock('../../services/authService', () => ({
  getUser: vi.fn(),
  getToken: vi.fn(),
  logout: vi.fn(),
}));

const mockWorkspace = {
  id: 'ws-test',
  name: 'Test Workspace',
  isAiSummarizationEnabled: false,
  isCustomerSatisfactionAnalysisEnabled: false,
  ownerId: 'user-1',
};

const defaultProps = {
  workspaces: [mockWorkspace],
  activeWorkspaceId: 'ws-test',
  onSwitchWorkspace: vi.fn(),
  onDeleteWorkspace: vi.fn(),
  onLogout: vi.fn(),
  isOpen: true,
  onClose: vi.fn(),
};

const renderSidebarAt = (pathname: string) =>
  render(
    <MemoryRouter initialEntries={[pathname]}>
      <Sidebar {...defaultProps} />
    </MemoryRouter>
  );

describe('MCP Servers nav item active state', () => {
  beforeEach(() => {
    vi.mocked(authService.getUser).mockReturnValue({
      id: 'user-1',
      email: 'test@example.com',
      name: 'Test User',
    });
  });

  it('is_active_when_on_mcp_servers_list_route', () => {
    renderSidebarAt('/workspaces/ws-test/mcp-servers');

    const mcpLink = screen.getByRole('link', { name: /mcp servers/i });
    expect(mcpLink.className).toContain('text-primary');
  });

  it('is_active_when_on_mcp_servers_new_route', () => {
    renderSidebarAt('/workspaces/ws-test/mcp-servers/new');

    const mcpLink = screen.getByRole('link', { name: /mcp servers/i });
    expect(mcpLink.className).toContain('text-primary');
  });

  it('is_active_when_on_mcp_servers_edit_route', () => {
    renderSidebarAt('/workspaces/ws-test/mcp-servers/abc123/edit');

    const mcpLink = screen.getByRole('link', { name: /mcp servers/i });
    expect(mcpLink.className).toContain('text-primary');
  });

  it('is_not_active_when_on_integrations_route', () => {
    renderSidebarAt('/workspaces/ws-test/integrations');

    const mcpLink = screen.getByRole('link', { name: /mcp servers/i });
    expect(mcpLink.className).not.toContain('text-primary');
  });

  it('is_not_active_when_on_tickets_route', () => {
    renderSidebarAt('/workspaces/ws-test/tickets');

    const mcpLink = screen.getByRole('link', { name: /mcp servers/i });
    expect(mcpLink.className).not.toContain('text-primary');
  });

  it('is_not_active_when_on_agents_route', () => {
    renderSidebarAt('/workspaces/ws-test/agents');

    const mcpLink = screen.getByRole('link', { name: /mcp servers/i });
    expect(mcpLink.className).not.toContain('text-primary');
  });
});

describe('Integrations nav item does not activate on MCP Servers routes', () => {
  beforeEach(() => {
    vi.mocked(authService.getUser).mockReturnValue({
      id: 'user-1',
      email: 'test@example.com',
      name: 'Test User',
    });
  });

  it('integrations_item_is_not_active_when_on_mcp_servers_new', () => {
    renderSidebarAt('/workspaces/ws-test/mcp-servers/new');

    const integrationsLink = screen.getByRole('link', { name: /^integrations$/i });
    expect(integrationsLink.className).not.toContain('text-primary');
  });

  it('integrations_item_is_active_when_on_integrations_route', () => {
    renderSidebarAt('/workspaces/ws-test/integrations');

    const integrationsLink = screen.getByRole('link', { name: /^integrations$/i });
    expect(integrationsLink.className).toContain('text-primary');
  });
});
