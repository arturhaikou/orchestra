import React from 'react';
import { render, screen, waitFor, fireEvent, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import McpServersPage from '../McpServersPage';
import { McpServer } from '../../../types';
import { DeleteServerOutcome } from '../../../hooks/useMcpServers';

// ─── Mock useMcpServers ───────────────────────────────────────────────────────
const mockDeleteServer = vi.fn<(id: string) => Promise<DeleteServerOutcome>>();
const mockRemoveServer = vi.fn();
const mockRetry = vi.fn();

vi.mock('../../../hooks/useMcpServers', () => ({
  useMcpServers: vi.fn(),
}));

import { useMcpServers } from '../../../hooks/useMcpServers';

const makeServer = (overrides: Partial<McpServer> = {}): McpServer => ({
  id: 'srv-1',
  workspaceId: 'ws-test',
  name: 'My Analytics Server',
  connectionStatus: 'Connected',
  transportType: 'HTTP',
  endpointUrl: 'https://mcp.example.com',
  createdAt: '2026-01-01T00:00:00.000Z',
  ...overrides,
});

const setupHook = (servers: McpServer[] = [makeServer()]) => {
  vi.mocked(useMcpServers).mockReturnValue({
    servers,
    isLoading: false,
    hasError: false,
    retry: mockRetry,
    removeServer: mockRemoveServer,
    deleteServer: mockDeleteServer,
    fetchImpact: vi.fn().mockResolvedValue(0),
  });
};

const renderPage = () =>
  render(
    <MemoryRouter initialEntries={['/workspaces/ws-test/mcp-servers']}>
      <Routes>
        <Route path="/workspaces/:workspaceId/mcp-servers" element={<McpServersPage />} />
      </Routes>
    </MemoryRouter>,
  );

beforeEach(() => {
  vi.clearAllMocks();
});

describe('Scenario 1: Clicking delete icon opens the confirmation modal', () => {
  it('DeleteIcon_OnClick_OpensModalWithTitle', async () => {
    setupHook();
    renderPage();

    const deleteButton = screen.getByRole('button', { name: /delete server/i });
    await userEvent.click(deleteButton);

    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('Delete MCP Server?')).toBeInTheDocument();
  });

  it('DeleteIcon_OnClick_ModalShowsServerName', async () => {
    setupHook([makeServer({ name: 'My Analytics Server' })]);
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: /delete server/i }));

    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByText(/My Analytics Server/)).toBeInTheDocument();
  });
});

describe('Scenario 2: Cancelling the modal leaves the server intact', () => {
  it('CancelButton_OnClick_ClosesModal', async () => {
    setupHook();
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: /delete server/i }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /cancel/i }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('CancelButton_OnClick_ServerCardRemainsVisible', async () => {
    setupHook();
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: /delete server/i }));
    await userEvent.click(screen.getByRole('button', { name: /cancel/i }));

    expect(screen.getByText('My Analytics Server')).toBeInTheDocument();
  });

  it('CancelButton_DoesNotCallDeleteServer', async () => {
    setupHook();
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: /delete server/i }));
    await userEvent.click(screen.getByRole('button', { name: /cancel/i }));

    expect(mockDeleteServer).not.toHaveBeenCalled();
  });
});

describe('Scenario 4: Confirming deletion removes card and shows success toast', () => {
  it('DeleteConfirm_OnSuccess_ClosesModal', async () => {
    mockDeleteServer.mockResolvedValue({ success: true, errorMessage: null });
    setupHook();
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: /delete server/i }));
    await userEvent.click(screen.getByRole('button', { name: /^delete$/i }));

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
  });

  it('DeleteConfirm_OnSuccess_ShowsSuccessToast', async () => {
    mockDeleteServer.mockResolvedValue({ success: true, errorMessage: null });
    setupHook([makeServer({ name: 'My Analytics Server' })]);
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: /delete server/i }));
    await userEvent.click(screen.getByRole('button', { name: /^delete$/i }));

    await waitFor(() => {
      expect(
        screen.getByText(/MCP Server 'My Analytics Server' deleted/i),
      ).toBeInTheDocument();
    });
  });

  it('DeleteConfirm_OnSuccess_CallsDeleteServerWithId', async () => {
    mockDeleteServer.mockResolvedValue({ success: true, errorMessage: null });
    setupHook([makeServer({ id: 'srv-42' })]);
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: /delete server/i }));
    await userEvent.click(screen.getByRole('button', { name: /^delete$/i }));

    await waitFor(() => expect(mockDeleteServer).toHaveBeenCalledWith('srv-42'));
  });
});

describe('Scenario 5: Deleting the last server transitions to empty state', () => {
  it('LastServer_OnDeleteSuccess_ShowsEmptyState', async () => {
    mockDeleteServer.mockImplementation(async (id: string) => {
      // Simulate the hook removing the server from state
      vi.mocked(useMcpServers).mockReturnValue({
        servers: [],
        isLoading: false,
        hasError: false,
        retry: mockRetry,
        removeServer: mockRemoveServer,
        deleteServer: mockDeleteServer,
        fetchImpact: vi.fn().mockResolvedValue(0),
      });
      return { success: true, errorMessage: null };
    });

    setupHook([makeServer()]);
    const { rerender } = renderPage();

    await userEvent.click(screen.getByRole('button', { name: /delete server/i }));
    await userEvent.click(screen.getByRole('button', { name: /^delete$/i }));

    // Re-render after state update
    rerender(
      <MemoryRouter initialEntries={['/workspaces/ws-test/mcp-servers']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/mcp-servers" element={<McpServersPage />} />
        </Routes>
      </MemoryRouter>,
    );

    await waitFor(() => {
      // Empty state renders when servers.length === 0
      expect(screen.queryByRole('button', { name: /delete server/i })).not.toBeInTheDocument();
    });
  });
});

describe('Scenario 6: Deletion failure shows error inside the modal', () => {
  it('DeleteConfirm_OnFailure_ModalRemainsOpen', async () => {
    mockDeleteServer.mockResolvedValue({
      success: false,
      errorMessage: "Failed to delete 'My Analytics Server'. Please try again.",
    });
    setupHook();
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: /delete server/i }));
    await userEvent.click(screen.getByRole('button', { name: /^delete$/i }));

    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });
  });

  it('DeleteConfirm_OnFailure_ShowsErrorMessageInModal', async () => {
    mockDeleteServer.mockResolvedValue({
      success: false,
      errorMessage: "Failed to delete 'My Analytics Server'. Please try again.",
    });
    setupHook();
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: /delete server/i }));
    await userEvent.click(screen.getByRole('button', { name: /^delete$/i }));

    await waitFor(() => {
      expect(
        screen.getByText(/Failed to delete 'My Analytics Server'/i),
      ).toBeInTheDocument();
    });
  });

  it('DeleteConfirm_OnFailure_DoesNotShowSuccessToast', async () => {
    mockDeleteServer.mockResolvedValue({
      success: false,
      errorMessage: "Failed to delete 'My Analytics Server'. Please try again.",
    });
    setupHook();
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: /delete server/i }));
    await userEvent.click(screen.getByRole('button', { name: /^delete$/i }));

    await waitFor(() => expect(mockDeleteServer).toHaveBeenCalled());
    expect(
      screen.queryByText(/MCP Server 'My Analytics Server' deleted/i),
    ).not.toBeInTheDocument();
  });
});
