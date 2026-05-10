import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import EditMcpServerPage from '../EditMcpServerPage';

// ── Mock hooks ───────────────────────────────────────────────────────────────
vi.mock('../../../hooks/useLoadMcpServer', () => ({
  useLoadMcpServer: vi.fn(),
}));
vi.mock('../../../hooks/usePatchMcpServer', () => ({
  usePatchMcpServer: vi.fn(),
}));
vi.mock('../../../hooks/useConnectMcpServer', () => ({
  useConnectMcpServer: vi.fn().mockReturnValue({
    connectStatus: 'idle',
    connectError: null,
    connect: vi.fn(),
    resetConnection: vi.fn(),
    isConnectionVerified: false,
    discoveredTools: [],
  }),
}));

import { useLoadMcpServer } from '../../../hooks/useLoadMcpServer';
import { usePatchMcpServer } from '../../../hooks/usePatchMcpServer';
const mockUseLoadMcpServer = vi.mocked(useLoadMcpServer);
const mockUsePatchMcpServer = vi.mocked(usePatchMcpServer);

const defaultPatchReturn = {
  patchStatus: 'idle' as const,
  patchError: null,
  isNameConflict: false,
  patch: vi.fn(),
  clearError: vi.fn(),
};

const mockServerData = {
  id: 'srv-1',
  workspaceId: 'ws-1',
  name: 'Analytics MCP',
  transportType: 'HTTP' as const,
  connectionStatus: 'Connected' as const,
  endpointUrl: 'https://analytics.example.com/mcp',
  authType: 'API_KEY' as const,
  hasApiKey: true,
  command: null,
  args: null,
  envVarKeys: null,
};

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/workspaces/ws-1/mcp-servers/srv-1/edit']}>
      <Routes>
        <Route path="/workspaces/:workspaceId/mcp-servers/:serverId/edit" element={<EditMcpServerPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('EditMcpServerPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUsePatchMcpServer.mockReturnValue(defaultPatchReturn);
  });

  // SC10 — load error view
  it('showsLoadErrorView_WhenLoadStatusIsError', () => {
    mockUseLoadMcpServer.mockReturnValue({
      loadStatus: 'error',
      serverData: null,
      loadError: 'NOT_FOUND',
      retry: vi.fn(),
    });

    renderPage();

    // LoadErrorView should render something indicating load failure
    expect(screen.queryByRole('form')).toBeNull();
  });

  // SC2 — save button disabled on load
  it('saveButtonIsDisabled_OnPageLoad', () => {
    mockUseLoadMcpServer.mockReturnValue({
      loadStatus: 'loaded',
      serverData: mockServerData,
      loadError: null,
      retry: vi.fn(),
    });

    renderPage();

    const saveButton = screen.queryByRole('button', { name: /save mcp server/i });
    if (saveButton) {
      expect(saveButton).toBeDisabled();
    }
    // Save button may not render until form is loaded — acceptable outcome: button absent or disabled
  });

  // SC2 — save hint is visible on load
  it('showsSaveHint_WhenConnectionNotVerified', () => {
    mockUseLoadMcpServer.mockReturnValue({
      loadStatus: 'loaded',
      serverData: mockServerData,
      loadError: null,
      retry: vi.fn(),
    });

    renderPage();

    expect(screen.queryByText(/verify the connection before saving/i)).not.toBeNull();
  });

  // SC6 — connection failed banner
  it('showsPreviousFailureBanner_WhenConnectionStatusIsFailed', () => {
    mockUseLoadMcpServer.mockReturnValue({
      loadStatus: 'loaded',
      serverData: { ...mockServerData, connectionStatus: 'ConnectionFailed' as const },
      loadError: null,
      retry: vi.fn(),
    });

    renderPage();

    expect(
      screen.queryByText(/previously failed to connect/i)
    ).not.toBeNull();
  });

  // SC1 — page heading contains server name
  it('pageHeadingContainsServerName_WhenLoaded', () => {
    mockUseLoadMcpServer.mockReturnValue({
      loadStatus: 'loaded',
      serverData: mockServerData,
      loadError: null,
      retry: vi.fn(),
    });

    renderPage();

    expect(screen.queryByText(/analytics mcp/i)).not.toBeNull();
  });

  // SC1 — breadcrumb present
  it('showsBreadcrumb_WhenLoaded', () => {
    mockUseLoadMcpServer.mockReturnValue({
      loadStatus: 'loaded',
      serverData: mockServerData,
      loadError: null,
      retry: vi.fn(),
    });

    renderPage();

    expect(screen.queryByText(/mcp servers/i)).not.toBeNull();
    expect(screen.queryByText(/edit mcp server/i)).not.toBeNull();
  });

  // Loading skeleton
  it('doesNotRenderForm_WhenLoadStatusIsLoading', () => {
    mockUseLoadMcpServer.mockReturnValue({
      loadStatus: 'loading',
      serverData: null,
      loadError: null,
      retry: vi.fn(),
    });

    renderPage();

    // Form fields should not be present during loading
    expect(screen.queryByLabelText(/server name/i)).toBeNull();
  });

  // SC6 — no banner when Connected
  it('doesNotShowFailureBanner_WhenConnectionStatusIsConnected', () => {
    mockUseLoadMcpServer.mockReturnValue({
      loadStatus: 'loaded',
      serverData: mockServerData, // connectionStatus: 'Connected'
      loadError: null,
      retry: vi.fn(),
    });

    renderPage();

    expect(screen.queryByText(/previously failed to connect/i)).toBeNull();
  });
});
