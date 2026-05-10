import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import McpServersPage from '../pages/McpServersPage';
import * as integrationService from '../../services/integrationService';
import { McpServer } from '../../types';

vi.mock('../../services/integrationService', () => ({
  getMcpServers: vi.fn(),
  deleteMcpServer: vi.fn(),
  getIntegrations: vi.fn(),
  createIntegration: vi.fn(),
  updateIntegration: vi.fn(),
  deleteIntegration: vi.fn(),
  testIntegrationConnection: vi.fn(),
}));

const mockServer = (overrides: Partial<McpServer> = {}): McpServer => ({
  id: 'server-1',
  workspaceId: 'ws-test',
  name: 'Test Server',
  connectionStatus: 'Connected',
  transportType: 'HTTP',
  endpointUrl: 'https://mcp.example.com',
  createdAt: '2026-04-28T10:22:00.000Z',
  ...overrides,
});

const CreatePagePlaceholder = () => <div data-testid="create-page">Add MCP Server Page</div>;
const EditPagePlaceholder = () => <div data-testid="edit-page">Edit MCP Server Page</div>;

const renderPage = (workspaceId = 'ws-test') => {
  return render(
    <MemoryRouter initialEntries={[`/workspaces/${workspaceId}/mcp-servers`]}>
      <Routes>
        <Route path="/workspaces/:workspaceId/mcp-servers" element={<McpServersPage />} />
        <Route path="/workspaces/:workspaceId/mcp-servers/new" element={<CreatePagePlaceholder />} />
        <Route path="/workspaces/:workspaceId/mcp-servers/:serverId/edit" element={<EditPagePlaceholder />} />
      </Routes>
    </MemoryRouter>
  );
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe('McpServersPage', () => {
  describe('Scenario 1: Empty state when no servers exist', () => {
    it('renders_empty_state_heading_when_no_servers', async () => {
      vi.mocked(integrationService.getMcpServers).mockResolvedValue([]);

      renderPage();

      await waitFor(() =>
        expect(screen.getByText('No MCP Servers yet')).toBeInTheDocument()
      );
    });

    it('renders_page_heading_in_empty_state', async () => {
      vi.mocked(integrationService.getMcpServers).mockResolvedValue([]);

      renderPage();

      await waitFor(() =>
        expect(screen.getByRole('heading', { name: 'MCP Servers' })).toBeInTheDocument()
      );
    });

    it('renders_add_button_in_empty_state', async () => {
      vi.mocked(integrationService.getMcpServers).mockResolvedValue([]);

      renderPage();

      await waitFor(() => expect(screen.getByText('No MCP Servers yet')).toBeInTheDocument());

      expect(screen.getAllByText('Add MCP Server').length).toBeGreaterThanOrEqual(1);
    });
  });

  describe('Scenario 2: Cards displayed for each server', () => {
    it('renders_card_for_each_server', async () => {
      const servers = [
        mockServer({ id: 'server-1', name: 'Figma MCP' }),
        mockServer({ id: 'server-2', name: 'GitHub MCP' }),
        mockServer({ id: 'server-3', name: 'Local Dev Tools', transportType: 'STDIO' }),
      ];
      vi.mocked(integrationService.getMcpServers).mockResolvedValue(servers);

      renderPage();

      await waitFor(() => expect(screen.getByText('Figma MCP')).toBeInTheDocument());

      expect(screen.getByText('GitHub MCP')).toBeInTheDocument();
      expect(screen.getByText('Local Dev Tools')).toBeInTheDocument();
    });

    it('renders_add_button_in_header_when_servers_exist', async () => {
      vi.mocked(integrationService.getMcpServers).mockResolvedValue([mockServer()]);

      renderPage();

      await waitFor(() => expect(screen.getByText('Test Server')).toBeInTheDocument());

      expect(screen.getByRole('link', { name: /add mcp server/i })).toBeInTheDocument();
    });
  });

  describe('Scenario 3: Skeleton loading state', () => {
    it('renders_skeleton_while_loading', () => {
      vi.mocked(integrationService.getMcpServers).mockImplementation(
        () => new Promise(() => {}) // never resolves
      );

      renderPage();

      expect(screen.getByRole('heading', { name: 'MCP Servers' })).toBeInTheDocument();
      expect(screen.queryByText('Test Server')).not.toBeInTheDocument();
    });

    it('renders_add_button_during_loading', () => {
      vi.mocked(integrationService.getMcpServers).mockImplementation(
        () => new Promise(() => {})
      );

      renderPage();

      expect(screen.getByRole('link', { name: /add mcp server/i })).toBeInTheDocument();
    });
  });

  describe('Scenario 4: Error state when loading fails', () => {
    it('renders_error_banner_when_fetch_fails', async () => {
      vi.mocked(integrationService.getMcpServers).mockRejectedValue(new Error('HTTP 500'));

      renderPage();

      await waitFor(() =>
        expect(
          screen.getByText(/could not load mcp servers/i)
        ).toBeInTheDocument()
      );
    });

    it('renders_retry_button_in_error_state', async () => {
      vi.mocked(integrationService.getMcpServers).mockRejectedValue(new Error('HTTP 500'));

      renderPage();

      await waitFor(() => screen.getByText(/could not load mcp servers/i));

      expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
    });

    it('renders_add_button_remains_accessible_in_error_state', async () => {
      vi.mocked(integrationService.getMcpServers).mockRejectedValue(new Error('HTTP 500'));

      renderPage();

      await waitFor(() => screen.getByText(/could not load mcp servers/i));

      expect(screen.getByRole('link', { name: /add mcp server/i })).toBeInTheDocument();
    });

    it('clicking_retry_refetches_server_list', async () => {
      vi.mocked(integrationService.getMcpServers)
        .mockRejectedValueOnce(new Error('HTTP 500'))
        .mockResolvedValue([mockServer({ name: 'Recovered Server' })]);

      renderPage();

      await waitFor(() => screen.getByText(/could not load mcp servers/i));

      await userEvent.click(screen.getByRole('button', { name: /retry/i }));

      await waitFor(() =>
        expect(screen.getByText('Recovered Server')).toBeInTheDocument()
      );
    });
  });

  describe('Scenario 5: Add MCP Server navigates to create page', () => {
    it('clicking_add_button_navigates_to_create_page', async () => {
      vi.mocked(integrationService.getMcpServers).mockResolvedValue([]);

      renderPage();

      await waitFor(() => screen.getByText('No MCP Servers yet'));

      const addButtons = screen.getAllByText('Add MCP Server');
      await userEvent.click(addButtons[0]);

      await waitFor(() =>
        expect(screen.getByTestId('create-page')).toBeInTheDocument()
      );
    });
  });

  describe('Scenario 7: Delete icon opens confirmation modal', () => {
    it('clicking_delete_icon_opens_confirmation_modal', async () => {
      vi.mocked(integrationService.getMcpServers).mockResolvedValue([
        mockServer({ id: 'server-1', name: 'My Server' }),
      ]);

      renderPage();

      await waitFor(() => expect(screen.getByText('My Server')).toBeInTheDocument());

      const deleteButton = screen.getByRole('button', { name: /delete server/i });
      await userEvent.click(deleteButton);

      expect(screen.getByRole('heading', { name: 'Delete MCP Server' })).toBeInTheDocument();
    });

    it('delete_modal_body_references_server_name', async () => {
      vi.mocked(integrationService.getMcpServers).mockResolvedValue([
        mockServer({ id: 'server-1', name: 'My Server' }),
      ]);

      renderPage();

      await waitFor(() => expect(screen.getByText('My Server')).toBeInTheDocument());

      const deleteButton = screen.getByRole('button', { name: /delete server/i });
      await userEvent.click(deleteButton);

      expect(screen.getByText((content, el) => el?.tagName === 'STRONG' && content === 'My Server')).toBeInTheDocument();
    });
  });

  describe('FR-002 Scenario 1: Card displays all required information', () => {
    it('card_shows_server_name', async () => {
      vi.mocked(integrationService.getMcpServers).mockResolvedValue([
        mockServer({ name: 'My Figma Server', connectionStatus: 'Connected', transportType: 'HTTP' }),
      ]);

      renderPage();

      await waitFor(() => expect(screen.getByText('My Figma Server')).toBeInTheDocument());
    });

    it('card_shows_connection_status_badge', async () => {
      vi.mocked(integrationService.getMcpServers).mockResolvedValue([
        mockServer({ connectionStatus: 'Connected' }),
      ]);

      renderPage();

      await waitFor(() =>
        expect(screen.getByLabelText('Connection status: Connected')).toBeInTheDocument()
      );
    });

    it('card_shows_transport_badge', async () => {
      vi.mocked(integrationService.getMcpServers).mockResolvedValue([
        mockServer({ transportType: 'HTTP' }),
      ]);

      renderPage();

      await waitFor(() =>
        expect(screen.getByLabelText('Transport: HTTP')).toBeInTheDocument()
      );
    });

    it('card_shows_edit_icon_button', async () => {
      vi.mocked(integrationService.getMcpServers).mockResolvedValue([mockServer()]);

      renderPage();

      await waitFor(() => expect(screen.getByText('Test Server')).toBeInTheDocument());

      expect(screen.getByRole('button', { name: 'Edit server' })).toBeInTheDocument();
    });

    it('card_shows_delete_icon_button', async () => {
      vi.mocked(integrationService.getMcpServers).mockResolvedValue([mockServer()]);

      renderPage();

      await waitFor(() => expect(screen.getByText('Test Server')).toBeInTheDocument());

      expect(screen.getByRole('button', { name: 'Delete server' })).toBeInTheDocument();
    });
  });

  describe('FR-002 Scenario 4: Clicking Edit navigates to edit page', () => {
    it('clicking_edit_button_navigates_to_edit_page', async () => {
      vi.mocked(integrationService.getMcpServers).mockResolvedValue([
        mockServer({ id: 'srv-abc', name: 'My Figma Server' }),
      ]);

      renderPage('ws-test');

      await waitFor(() => expect(screen.getByText('My Figma Server')).toBeInTheDocument());

      await userEvent.click(screen.getByRole('button', { name: 'Edit server' }));

      await waitFor(() =>
        expect(screen.getByTestId('edit-page')).toBeInTheDocument()
      );
    });
  });

  describe('FR-002 Scenario 6: Keyboard navigation on icon buttons', () => {
    it('pressing_enter_on_edit_button_navigates_to_edit_page', async () => {
      vi.mocked(integrationService.getMcpServers).mockResolvedValue([
        mockServer({ id: 'srv-abc', name: 'My Figma Server' }),
      ]);

      renderPage('ws-test');

      await waitFor(() => expect(screen.getByText('My Figma Server')).toBeInTheDocument());

      const editButton = screen.getByRole('button', { name: 'Edit server' });
      editButton.focus();
      await userEvent.keyboard('{Enter}');

      await waitFor(() =>
        expect(screen.getByTestId('edit-page')).toBeInTheDocument()
      );
    });

    it('pressing_enter_on_delete_button_opens_delete_modal', async () => {
      vi.mocked(integrationService.getMcpServers).mockResolvedValue([
        mockServer({ name: 'My Figma Server' }),
      ]);

      renderPage();

      await waitFor(() => expect(screen.getByText('My Figma Server')).toBeInTheDocument());

      const deleteButton = screen.getByRole('button', { name: 'Delete server' });
      deleteButton.focus();
      await userEvent.keyboard('{Enter}');

      expect(screen.getByRole('heading', { name: 'Delete MCP Server' })).toBeInTheDocument();
    });
  });
});
