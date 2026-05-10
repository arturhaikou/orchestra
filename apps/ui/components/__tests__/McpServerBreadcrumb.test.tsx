import React from 'react';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect } from 'vitest';
import McpServerBreadcrumb from '../mcp/McpServerBreadcrumb';

const renderBreadcrumb = (workspaceId = 'ws-test') =>
  render(
    <MemoryRouter>
      <McpServerBreadcrumb workspaceId={workspaceId} />
    </MemoryRouter>
  );

describe('McpServerBreadcrumb', () => {
  it('renders_mcp_servers_as_a_link', () => {
    renderBreadcrumb();

    expect(screen.getByRole('link', { name: /mcp servers/i })).toBeInTheDocument();
  });

  it('mcp_servers_link_points_to_list_page', () => {
    renderBreadcrumb('ws-abc');

    const link = screen.getByRole('link', { name: /mcp servers/i });
    expect(link).toHaveAttribute('href', '/workspaces/ws-abc/mcp-servers');
  });

  it('renders_add_mcp_server_as_non_interactive_text', () => {
    renderBreadcrumb();

    const plainText = screen.getByText('Add MCP Server');
    expect(plainText.tagName.toLowerCase()).not.toBe('a');
  });

  it('breadcrumb_nav_has_accessible_label', () => {
    renderBreadcrumb();

    expect(screen.getByRole('navigation', { name: /breadcrumb/i })).toBeInTheDocument();
  });
});
