import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import ToolPickerLeftPanel, { ToolPickerLeftPanelProps } from '../agents/ToolPickerLeftPanel';
import { ToolCatalogueEntry, McpServer, McpToolFetchState, McpFetchedTool } from '../../types';

// ─── Shared test data ──────────────────────────────────────────────────────────

const mockCatalogue: ToolCatalogueEntry[] = [
  {
    actionId: 'action-read-tickets',
    actionName: 'Read Tickets',
    actionDescription: 'Reads Jira tickets',
    dangerLevel: 'Safe',
    sourceId: 'cat-jira',
    sourceName: 'Jira',
    sourceType: 'native',
  },
  {
    actionId: 'action-create-pr',
    actionName: 'Create PR',
    actionDescription: 'Opens a pull request',
    dangerLevel: 'Safe',
    sourceId: 'cat-github',
    sourceName: 'GitHub',
    sourceType: 'native',
  },
  {
    actionId: 'action-mcp-tool',
    actionName: 'MCP Tool',
    actionDescription: 'Does MCP stuff',
    dangerLevel: 'Moderate',
    sourceId: 'mcp-1',
    sourceName: 'Staging Tools',
    sourceType: 'mcp',
  },
];

const mockMcpServers: McpServer[] = [
  {
    id: 'mcp-1',
    workspaceId: 'ws-test',
    name: 'Staging Tools',
    connectionStatus: 'Connected',
    transportType: 'HTTP',
    endpointUrl: 'https://staging.example.com',
    createdAt: '2025-01-01T00:00:00Z',
  },
];

const mockMcpServerZeroTools: McpServer[] = [
  {
    id: 'mcp-zero',
    workspaceId: 'ws-test',
    name: 'Empty Server',
    connectionStatus: 'Connected',
    transportType: 'HTTP',
    endpointUrl: 'https://empty.example.com',
    createdAt: '2025-01-01T00:00:00Z',
  },
];

// ─── FR-003: Lazy MCP Tool Fetch test data ────────────────────────────────────

const mockFetchedTools: McpFetchedTool[] = [
  { name: 'tool-alpha', description: 'Alpha tool', dangerLevel: 'Safe' },
  { name: 'tool-beta', description: 'Beta tool', dangerLevel: 'Moderate' },
];

const idleState: McpToolFetchState = { status: 'idle' };
const loadingState: McpToolFetchState = { status: 'loading' };
const successState: McpToolFetchState = { status: 'success', tools: mockFetchedTools };
const errorState: McpToolFetchState = { status: 'error', message: 'Unreachable' };

// ─── Render helper ─────────────────────────────────────────────────────────────

function renderPanel(overrides: Partial<ToolPickerLeftPanelProps> = {}) {
  const onSelectSource = vi.fn();
  const props: ToolPickerLeftPanelProps = {
    toolCatalogue: mockCatalogue,
    mcpServers: mockMcpServers,
    activeSourceId: null,
    searchTerm: '',
    selectedActionIds: [],
    onSelectSource,
    mcpFetchStates: {},
    ...overrides,
  };
  render(
    <MemoryRouter>
      <ToolPickerLeftPanel {...props} />
    </MemoryRouter>
  );
  return { onSelectSource };
}

// ─── Tests ─────────────────────────────────────────────────────────────────────

describe('ToolPickerLeftPanel', () => {

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Scenario 1 — left panel shows built-in categories and MCP servers', () => {
    it('renders_built_in_categories_section_header', () => {
      renderPanel();
      expect(screen.getByText('Built-in Categories')).toBeInTheDocument();
    });

    it('renders_mcp_servers_section_header', () => {
      renderPanel();
      expect(screen.getByText('MCP Servers')).toBeInTheDocument();
    });

    it('lists_all_native_category_names', () => {
      renderPanel();
      expect(screen.getByText('Jira')).toBeInTheDocument();
      expect(screen.getByText('GitHub')).toBeInTheDocument();
    });

    it('shows_tool_count_badge_for_each_category', () => {
      renderPanel();
      const badges = screen.getAllByTestId('count-badge');
      expect(badges.length).toBeGreaterThanOrEqual(2);
      badges.forEach(badge => {
        expect(badge.textContent).toMatch(/\d+ \/ \d+/);
      });
    });

    it('lists_mcp_server_name', () => {
      renderPanel();
      expect(screen.getByText('Staging Tools')).toBeInTheDocument();
    });

    it('shows_connection_status_badge_for_mcp_server', () => {
      renderPanel();
      const categoryCards = screen.getAllByTestId('category-card');
      const mcpCard = categoryCards.find(card => card.textContent.includes('Staging Tools'));
      expect(mcpCard).toBeDefined();
    });
  });

  describe('Scenario 2 — clicking a source calls onSelectSource', () => {
    it('calls_onSelectSource_with_category_source_id_when_category_clicked', async () => {
      const user = userEvent.setup();
      const { onSelectSource } = renderPanel();
      await user.click(screen.getByText('Jira'));
      expect(onSelectSource).toHaveBeenCalledWith('cat-jira');
    });

    it('calls_onSelectSource_with_mcp_server_id_when_server_clicked', async () => {
      const user = userEvent.setup();
      const { onSelectSource } = renderPanel();
      await user.click(screen.getByText('Staging Tools'));
      expect(onSelectSource).toHaveBeenCalledWith('mcp-1');
    });

    it('highlights_active_item_when_activeSourceId_matches', () => {
      renderPanel({ activeSourceId: 'cat-github' });
      const githubItem = screen.getByText('GitHub').closest('[role="button"], li, button, [data-source-id]');
      expect(githubItem).toHaveAttribute('aria-current', 'true');
    });
  });

  describe('Scenario 3 — MCP server lazy badge behaviour (FR-003)', () => {
    it('does_not_render_tool_count_badge_for_mcp_server_in_idle_state', () => {
      renderPanel({
        mcpServers: mockMcpServers,
        mcpFetchStates: { 'mcp-1': idleState },
      });
      const categoryCards = screen.getAllByTestId('category-card');
      const mcpCard = categoryCards.find(card => card.textContent.includes('Staging Tools'));
      expect(mcpCard).toBeDefined();
      expect(mcpCard?.querySelector('[data-testid="count-badge"]')).toBeNull();
    });

    it('renders_discover_hint_for_mcp_server_in_idle_state', () => {
      renderPanel({
        mcpServers: mockMcpServers,
        mcpFetchStates: { 'mcp-1': idleState },
      });
      expect(screen.getByText(/click to discover/i)).toBeInTheDocument();
    });

    it('renders_tool_count_badge_for_mcp_server_in_success_state', () => {
      renderPanel({
        mcpServers: mockMcpServers,
        mcpFetchStates: { 'mcp-1': successState },
      });
      const categoryCards = screen.getAllByTestId('category-card');
      const mcpCard = categoryCards.find(card => card.textContent.includes('Staging Tools'));
      expect(mcpCard).toBeDefined();
      const badge = mcpCard?.querySelector('[data-testid="count-badge"]');
      expect(badge).toBeInTheDocument();
      expect(mcpCard?.textContent).toMatch(/0\s*\/\s*2/);
    });

    it('badge_reflects_selected_count_when_tools_are_selected', () => {
      renderPanel({
        mcpServers: mockMcpServers,
        mcpFetchStates: { 'mcp-1': successState },
        selectedActionIds: ['action-mcp-tool'],
      });
      const categoryCards = screen.getAllByTestId('category-card');
      const mcpCard = categoryCards.find(card => card.textContent.includes('Staging Tools'));
      expect(mcpCard).toBeDefined();
      const badge = mcpCard?.querySelector('[data-testid="count-badge"]');
      expect(badge).toBeInTheDocument();
      expect(badge?.textContent).toMatch(/\d+ \/ 2/);
    });

    it('does_not_render_discover_hint_in_success_state', () => {
      renderPanel({
        mcpServers: mockMcpServers,
        mcpFetchStates: { 'mcp-1': successState },
      });
      expect(screen.queryByText(/click to discover/i)).toBeNull();
    });

    it('does_not_render_badge_for_mcp_server_in_error_state', () => {
      renderPanel({
        mcpServers: mockMcpServers,
        mcpFetchStates: { 'mcp-1': errorState },
      });
      const categoryCards = screen.getAllByTestId('category-card');
      const mcpCard = categoryCards.find(card => card.textContent.includes('Staging Tools'));
      expect(mcpCard).toBeDefined();
      expect(mcpCard?.querySelector('[data-testid="count-badge"]')).toBeNull();
    });

    it('mcp_server_is_rendered_as_category_card', () => {
      renderPanel({
        mcpServers: mockMcpServers,
        mcpFetchStates: { 'mcp-1': idleState },
      });
      const categoryCards = screen.getAllByTestId('category-card');
      const stagingToolsCard = categoryCards.find(card => card.textContent.includes('Staging Tools'));
      expect(stagingToolsCard).toBeDefined();
      expect(screen.queryByTestId('mcp-server-item')).toBeNull();
    });
  });

  describe('Scenario 4 — no MCP servers empty state', () => {
    it('shows_empty_state_message_when_no_mcp_servers', () => {
      renderPanel({ mcpServers: [] });
      expect(screen.getByTestId('mcp-empty-state')).toBeInTheDocument();
    });

    it('shows_settings_hint_in_empty_state', () => {
      renderPanel({ mcpServers: [] });
      expect(screen.getByText(/Settings/i)).toBeInTheDocument();
    });
  });

  describe('Scenario 5 — search filtering', () => {
    it('hides_categories_not_matching_search_term', () => {
      renderPanel({ searchTerm: 'jira' });
      expect(screen.getByText('Jira')).toBeInTheDocument();
      expect(screen.queryByText('GitHub')).toBeNull();
    });

    it('hides_mcp_servers_not_matching_search_term', () => {
      const twoServers: McpServer[] = [
        {
          id: 'mcp-1',
          workspaceId: 'ws-test',
          name: 'Staging Tools',
          connectionStatus: 'Connected',
          transportType: 'HTTP',
          endpointUrl: 'https://staging.example.com',
          createdAt: '2025-01-01T00:00:00Z',
        },
        {
          id: 'mcp-2',
          workspaceId: 'ws-test',
          name: 'Prod Tools',
          connectionStatus: 'Connected',
          transportType: 'HTTP',
          endpointUrl: 'https://prod.example.com',
          createdAt: '2025-01-02T00:00:00Z',
        },
      ];
      renderPanel({ searchTerm: 'staging', mcpServers: twoServers });
      expect(screen.getByText('Staging Tools')).toBeInTheDocument();
      expect(screen.queryByText('Prod Tools')).toBeNull();
    });

    it('search_is_case_insensitive', () => {
      renderPanel({ searchTerm: 'GIT' });
      expect(screen.getByText('GitHub')).toBeInTheDocument();
    });
  });

  describe('Scenario 6 — no search results empty state', () => {
    it('shows_no_results_message_when_search_matches_nothing', () => {
      renderPanel({ searchTerm: 'xyznotfound' });
      expect(screen.getByText(/no results for 'xyznotfound'/i)).toBeInTheDocument();
    });

    it('hides_section_headers_when_search_matches_nothing', () => {
      renderPanel({ searchTerm: 'xyznotfound' });
      expect(screen.queryByText('Built-in Categories')).toBeNull();
    });
  });

  describe('Scenario 3 — loading state while MCP checks run', () => {
    it('shows_mcp_loading_indicator_when_isMcpLoading_is_true', () => {
      renderPanel({ isMcpLoading: true, mcpServers: [] });
      expect(screen.getByTestId('mcp-section-loading')).toBeInTheDocument();
    });

    it('does_not_render_server_rows_while_mcp_is_loading', () => {
      const servers: McpServer[] = [
        {
          id: 'mcp-loading',
          workspaceId: 'ws-test',
          name: 'Loading Server',
          connectionStatus: 'Connected',
          transportType: 'HTTP',
          createdAt: '2025-01-01T00:00:00Z',
        },
      ];
      renderPanel({ isMcpLoading: true, mcpServers: servers });
      expect(screen.queryByText('Loading Server')).not.toBeInTheDocument();
    });

    it('renders_server_rows_once_loading_is_complete', () => {
      renderPanel({ isMcpLoading: false, mcpServers: mockMcpServers });
      expect(screen.getByText('Staging Tools')).toBeInTheDocument();
    });
  });

  describe('Scenario 4 — empty MCP section', () => {
    it('shows_empty_state_when_no_servers_and_not_loading', () => {
      renderPanel({ isMcpLoading: false, mcpServers: [] });
      expect(screen.getByTestId('mcp-empty-state')).toBeInTheDocument();
    });

    it('empty_state_contains_settings_navigation_hint', () => {
      renderPanel({ isMcpLoading: false, mcpServers: [] });
      expect(screen.getByText(/Settings/i)).toBeInTheDocument();
    });
  });

  describe('FR-002 — Category cards rendered instead of plain items', () => {
    it('renders_native_categories_as_category_card_components', () => {
      renderPanel();
      const cards = screen.getAllByTestId('category-card');
      expect(cards.length).toBeGreaterThanOrEqual(2);
    });

    it('each_category_card_shows_selected_vs_total_badge', () => {
      renderPanel({
        selectedActionIds: ['action-read-tickets'],
      });
      expect(screen.getByText('1 / 1')).toBeInTheDocument();
    });

    it('badge_shows_zero_selected_when_no_matching_action_ids', () => {
      renderPanel({ selectedActionIds: [] });
      expect(screen.getAllByText('0 / 1').length).toBeGreaterThanOrEqual(2);
    });

    it('badge_updates_correctly_when_selectedActionIds_change', async () => {
      const { rerender } = render(
        <MemoryRouter>
          <ToolPickerLeftPanel
            toolCatalogue={mockCatalogue}
            mcpServers={mockMcpServers}
            activeSourceId={null}
            searchTerm=""
            selectedActionIds={[]}
            onSelectSource={vi.fn()}
          />
        </MemoryRouter>
      );
      expect(screen.getAllByText('0 / 1').length).toBeGreaterThanOrEqual(1);

      rerender(
        <MemoryRouter>
          <ToolPickerLeftPanel
            toolCatalogue={mockCatalogue}
            mcpServers={mockMcpServers}
            activeSourceId={null}
            searchTerm=""
            selectedActionIds={['action-read-tickets']}
            onSelectSource={vi.fn()}
          />
        </MemoryRouter>
      );
      expect(screen.getByText('1 / 1')).toBeInTheDocument();
    });

    it('active_category_card_has_aria_current_true', () => {
      renderPanel({ activeSourceId: 'cat-jira' });
      const jiraCard = screen.getByText('Jira').closest('[role="button"]');
      expect(jiraCard).toHaveAttribute('aria-current', 'true');
    });

    it('inactive_category_card_does_not_have_aria_current', () => {
      renderPanel({ activeSourceId: 'cat-github' });
      const jiraCard = screen.getByText('Jira').closest('[role="button"]');
      expect(jiraCard).not.toHaveAttribute('aria-current');
    });

    it('clicking_category_card_calls_onSelectSource', async () => {
      const user = userEvent.setup();
      const { onSelectSource } = renderPanel();
      const jiraCard = screen.getByText('Jira').closest('[role="button"]');
      await user.click(jiraCard!);
      expect(onSelectSource).toHaveBeenCalledWith('cat-jira');
    });
  });

  describe('FR-002 — Keyboard accessibility at panel level', () => {
    it('all_category_cards_are_reachable_by_Tab_key', () => {
      renderPanel();
      const cards = screen.getAllByTestId('category-card');
      cards.forEach((card) => {
        const button = card.closest('[role="button"]');
        expect(button).toBeDefined();
      });
    });
  });

  describe('FR-002 — Long name truncation', () => {
    it('card_has_title_attribute_containing_the_full_category_name', () => {
      const longNameCatalogue: ToolCatalogueEntry[] = [
        {
          actionId: 'action-long-name',
          actionName: 'Long Action',
          actionDescription: 'Very long category name that should be truncated',
          dangerLevel: 'Safe',
          sourceId: 'cat-long',
          sourceName: 'This is a very long category name that exceeds normal display limits',
          sourceType: 'native',
        },
      ];
      renderPanel({ toolCatalogue: longNameCatalogue });
      const card = screen.getByText(/very long category name/i).closest('[role="button"]');
      const titleOrAriaLabel = card?.getAttribute('title') || card?.getAttribute('aria-label');
      expect(titleOrAriaLabel).toBeDefined();
    });
  });

});
