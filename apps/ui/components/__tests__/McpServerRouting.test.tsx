import React from 'react';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route, Navigate, useParams, useLocation } from 'react-router-dom';
import { vi, describe, it, expect } from 'vitest';
import McpServersPage from '../pages/McpServersPage';
import CreateMcpServerPage from '../pages/CreateMcpServerPage';
import EditMcpServerPage from '../pages/EditMcpServerPage';

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return {
    ...actual,
    useBlocker: () => ({ state: 'unblocked', proceed: vi.fn(), reset: vi.fn() }),
  };
});

vi.mock('../../hooks/useMcpServers', () => ({
  useMcpServers: () => ({ servers: [], isLoading: false, error: null, refetch: vi.fn() }),
}));
vi.mock('../../hooks/useLoadMcpServer', () => ({
  useLoadMcpServer: () => ({ loadStatus: 'idle', serverData: null, loadError: null, retry: vi.fn() }),
}));
vi.mock('../../hooks/useSaveMcpServer', () => ({
  useSaveMcpServer: () => ({ saveStatus: 'idle', save: vi.fn() }),
}));
vi.mock('../../hooks/usePatchMcpServer', () => ({
  usePatchMcpServer: () => ({ patchStatus: 'idle', patch: vi.fn() }),
}));
vi.mock('../../hooks/useConnectMcpServer', () => ({
  useConnectMcpServer: () => ({ connectStatus: 'idle', connect: vi.fn() }),
}));

const RedirectToMcpNew: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  return <Navigate to={`/workspaces/${workspaceId}/mcp-servers/new`} replace />;
};

const RedirectToMcpEdit: React.FC = () => {
  const { workspaceId, integrationId } = useParams<{ workspaceId: string; integrationId: string }>();
  return <Navigate to={`/workspaces/${workspaceId}/mcp-servers/${integrationId}/edit`} replace />;
};

const LocationDisplay: React.FC = () => {
  const location = useLocation();
  return <div data-testid="current-path">{location.pathname}</div>;
};

const renderWithRoutes = (initialPath: string) =>
  render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/workspaces/:workspaceId">
          <Route path="mcp-servers/new" element={<CreateMcpServerPage />} />
          <Route path="mcp-servers/:serverId/edit" element={<EditMcpServerPage />} />
          <Route path="mcp-servers" element={<McpServersPage />} />
          <Route path="integrations/new/mcp" element={<RedirectToMcpNew />} />
          <Route path="integrations/:integrationId/edit/mcp" element={<RedirectToMcpEdit />} />
        </Route>
      </Routes>
      <LocationDisplay />
    </MemoryRouter>
  );

describe('Scenario 5: Legacy integration MCP URLs redirect to MCP Servers domain', () => {
  it('redirects_integrations_new_mcp_to_mcp_servers_new', () => {
    renderWithRoutes('/workspaces/ws-test/integrations/new/mcp');

    expect(screen.getByTestId('current-path').textContent).toBe(
      '/workspaces/ws-test/mcp-servers/new'
    );
  });

  it('does_not_show_error_page_on_legacy_create_url', () => {
    renderWithRoutes('/workspaces/ws-test/integrations/new/mcp');

    expect(screen.queryByText(/not found/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/error/i)).not.toBeInTheDocument();
  });

  it('redirects_integrations_id_edit_mcp_to_mcp_servers_id_edit', () => {
    renderWithRoutes('/workspaces/ws-test/integrations/abc123/edit/mcp');

    expect(screen.getByTestId('current-path').textContent).toBe(
      '/workspaces/ws-test/mcp-servers/abc123/edit'
    );
  });

  it('preserves_integration_id_in_redirect_to_mcp_edit', () => {
    renderWithRoutes('/workspaces/ws-test/integrations/server-xyz/edit/mcp');

    expect(screen.getByTestId('current-path').textContent).toBe(
      '/workspaces/ws-test/mcp-servers/server-xyz/edit'
    );
  });

  it('does_not_show_error_page_on_legacy_edit_url', () => {
    renderWithRoutes('/workspaces/ws-test/integrations/abc123/edit/mcp');

    expect(screen.queryByText(/not found/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/error/i)).not.toBeInTheDocument();
  });
});

describe('Scenario 4: Dedicated MCP Servers routes are accessible', () => {
  it('mcp_servers_list_route_is_reachable', () => {
    renderWithRoutes('/workspaces/ws-test/mcp-servers');

    expect(screen.getByTestId('current-path').textContent).toBe(
      '/workspaces/ws-test/mcp-servers'
    );
  });

  it('mcp_servers_new_route_is_reachable', () => {
    renderWithRoutes('/workspaces/ws-test/mcp-servers/new');

    expect(screen.getByTestId('current-path').textContent).toBe(
      '/workspaces/ws-test/mcp-servers/new'
    );
  });

  it('mcp_servers_edit_route_is_reachable_with_server_id', () => {
    renderWithRoutes('/workspaces/ws-test/mcp-servers/srv-001/edit');

    expect(screen.getByTestId('current-path').textContent).toBe(
      '/workspaces/ws-test/mcp-servers/srv-001/edit'
    );
  });
});
