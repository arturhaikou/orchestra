import React from 'react';
import { render, screen } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import McpIntegrationCard from '../mcp/McpIntegrationCard';
import { McpIntegration } from '../../types';

vi.mock('../../services/integrationService', () => ({
  syncIntegrationTools: vi.fn(),
  getIntegrations: vi.fn(),
  deleteIntegration: vi.fn(),
}));

const mockHttpIntegration: McpIntegration = {
  id: 'int-http-1',
  workspaceId: 'ws-test',
  name: 'HTTP MCP Server',
  provider: 'MCP_GENERIC',
  types: [],
  icon: 'mcp',
  isMcpBacked: true,
  mcpEndpointUrl: 'https://api.example.com/mcp',
  mcpAuthType: 'ApiKey',
  toolCount: 3,
  connected: true,
  lastSync: '1 hour ago',
  mcpTransportType: 'HTTP',
};

const mockStdioIntegration: McpIntegration = {
  id: 'int-stdio-1',
  workspaceId: 'ws-test',
  name: 'stdio MCP Server',
  provider: 'MCP_GENERIC',
  types: [],
  icon: 'mcp',
  isMcpBacked: true,
  mcpEndpointUrl: '',
  mcpAuthType: 'None',
  toolCount: 2,
  connected: true,
  lastSync: '2 hours ago',
  mcpTransportType: 'STDIO',
  mcpCommand: 'npx',
};

describe('McpIntegrationCard — FR-009 Transport Badge', () => {
  const mockOnSync = vi.fn();
  const mockOnEdit = vi.fn();
  const mockOnDelete = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders_transport_badge_for_http_integration', () => {
    render(
      <McpIntegrationCard
        integration={mockHttpIntegration}
        onSync={mockOnSync}
        onEdit={mockOnEdit}
        onDelete={mockOnDelete}
      />
    );

    expect(screen.getByLabelText('Transport: HTTP')).toBeInTheDocument();
  });

  it('renders_transport_badge_for_stdio_integration', () => {
    render(
      <McpIntegrationCard
        integration={mockStdioIntegration}
        onSync={mockOnSync}
        onEdit={mockOnEdit}
        onDelete={mockOnDelete}
      />
    );

    expect(screen.getByLabelText('Transport: stdio')).toBeInTheDocument();
  });

  it('shows_endpoint_url_as_subtitle_for_http_integration', () => {
    render(
      <McpIntegrationCard
        integration={mockHttpIntegration}
        onSync={mockOnSync}
        onEdit={mockOnEdit}
        onDelete={mockOnDelete}
      />
    );

    expect(screen.getByText('https://api.example.com/mcp')).toBeInTheDocument();
  });

  it('shows_command_as_subtitle_for_stdio_integration', () => {
    render(
      <McpIntegrationCard
        integration={mockStdioIntegration}
        onSync={mockOnSync}
        onEdit={mockOnEdit}
        onDelete={mockOnDelete}
      />
    );

    expect(screen.getByText('npx')).toBeInTheDocument();
  });
});
