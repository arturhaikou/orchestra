import React from 'react';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import AgentToolSummarySection, {
  AgentToolSummarySectionProps,
} from '../agents/AgentToolSummarySection';
import { ToolCatalogueEntry, McpServer, McpToolSelection } from '../../types';

// ─── Shared fixtures ───────────────────────────────────────────────────────────

const ticketTrackerTools: ToolCatalogueEntry[] = [
  { actionId: 'tt-read',   actionName: 'Read Tickets',   actionDescription: '', dangerLevel: 'Safe',        sourceId: 'cat-tracker', sourceName: 'Ticket Tracker', sourceType: 'native' },
  { actionId: 'tt-create', actionName: 'Create Ticket',  actionDescription: '', dangerLevel: 'Safe',        sourceId: 'cat-tracker', sourceName: 'Ticket Tracker', sourceType: 'native' },
  { actionId: 'tt-update', actionName: 'Update Ticket',  actionDescription: '', dangerLevel: 'Moderate',    sourceId: 'cat-tracker', sourceName: 'Ticket Tracker', sourceType: 'native' },
  { actionId: 'tt-delete', actionName: 'Delete Ticket',  actionDescription: '', dangerLevel: 'Destructive', sourceId: 'cat-tracker', sourceName: 'Ticket Tracker', sourceType: 'native' },
];

const codeReviewTools: ToolCatalogueEntry[] = [
  { actionId: 'cr-read',   actionName: 'Read PR',       actionDescription: '', dangerLevel: 'Safe',     sourceId: 'cat-cr', sourceName: 'Code Review', sourceType: 'native' },
  { actionId: 'cr-post',   actionName: 'Post Comment',  actionDescription: '', dangerLevel: 'Moderate', sourceId: 'cat-cr', sourceName: 'Code Review', sourceType: 'native' },
];

const figmaTools: ToolCatalogueEntry[] = [
  { actionId: 'fig-read',  actionName: 'Read Designs',   actionDescription: '', dangerLevel: 'Safe',     sourceId: 'mcp-figma', sourceName: 'Figma Tools', sourceType: 'mcp' },
  { actionId: 'fig-write', actionName: 'Update Designs', actionDescription: '', dangerLevel: 'Moderate', sourceId: 'mcp-figma', sourceName: 'Figma Tools', sourceType: 'mcp' },
];

const allTools: ToolCatalogueEntry[] = [...ticketTrackerTools, ...codeReviewTools, ...figmaTools];

const figmaServer: McpServer = {
  id: 'mcp-figma',
  workspaceId: 'ws-test',
  name: 'Figma Tools',
  connectionStatus: 'ConnectionFailed',
  transportType: 'HTTP',
  endpointUrl: 'https://figma.mcp.example.com',
  createdAt: '2025-01-01T00:00:00Z',
};

const alphaServer: McpServer = {
  id: 'server-abc',
  workspaceId: 'ws-test',
  name: 'Alpha Server',
  connectionStatus: 'Connected',
  transportType: 'HTTP',
  endpointUrl: 'https://alpha.mcp.example.com',
  createdAt: '2025-01-01T00:00:00Z',
};

const betaServer: McpServer = {
  id: 'server-xyz',
  workspaceId: 'ws-test',
  name: 'Beta Server',
  connectionStatus: 'Unverified',
  transportType: 'HTTP',
  endpointUrl: 'https://beta.mcp.example.com',
  createdAt: '2025-01-01T00:00:00Z',
};

function renderSection(overrides: Partial<AgentToolSummarySectionProps> = {}) {
  const onOpenModal = vi.fn();
  const onRemoveSource = vi.fn();
  const onRemoveMcpServer = vi.fn();
  const props: AgentToolSummarySectionProps = {
    toolActionIds: [],
    toolCatalogue: allTools,
    mcpServers: [figmaServer],
    mcpSelections: [],
    onOpenModal,
    onRemoveSource,
    onRemoveMcpServer,
    ...overrides,
  };
  const { rerender } = render(<AgentToolSummarySection {...props} />);
  return { onOpenModal, onRemoveSource, rerender, props };
}

// ─── Tests ─────────────────────────────────────────────────────────────────────

describe('AgentToolSummarySection', () => {
  beforeEach(() => vi.clearAllMocks());

  // ── Scenario 1: Card appears after selecting tools ──────────────────────────

  describe('Scenario 1 — card appears when tools are selected', () => {
    it('renders_a_card_for_a_source_with_selected_tools', () => {
      renderSection({ toolActionIds: ['tt-read', 'tt-create'] });
      expect(screen.getByText('Ticket Tracker')).toBeInTheDocument();
    });

    it('shows_correct_X_of_Y_count_on_the_card', () => {
      renderSection({ toolActionIds: ['tt-read', 'tt-create'] });
      const card = screen.getByTestId('tool-summary-card');
      const badge = within(card).getByTestId('selection-count');
      expect(badge).toHaveTextContent('2');
      expect(badge).toHaveTextContent('4');
    });
  });

  // ── Scenario 2: No card when 0 tools selected ────────────────────────────────

  describe('Scenario 2 — no card when source has zero selections', () => {
    it('does_not_render_a_card_for_a_source_with_no_selected_tools', () => {
      // Only Ticket Tracker selected — Code Review has none
      renderSection({ toolActionIds: ['tt-read'] });
      expect(screen.queryByText('Code Review')).not.toBeInTheDocument();
    });
  });

  // ── Scenario 3: ✕ removes all tools from source ─────────────────────────────

  describe('Scenario 3 — remove card via ✕', () => {
    it('calls_onRemoveSource_with_the_correct_sourceId_on_remove_click', async () => {
      const user = userEvent.setup();
      const { onRemoveSource } = renderSection({ toolActionIds: ['tt-read', 'tt-create'] });
      await user.click(screen.getByTestId('remove-source-button'));
      expect(onRemoveSource).toHaveBeenCalledWith('cat-tracker');
    });
  });

  // ── Scenario 4: Card body opens modal focused on source ──────────────────────

  describe('Scenario 4 — card body opens modal pre-focused on source', () => {
    it('calls_onOpenModal_with_sourceId_when_card_body_is_clicked', async () => {
      const user = userEvent.setup();
      const { onOpenModal } = renderSection({ toolActionIds: ['tt-read'] });
      await user.click(screen.getByTestId('tool-summary-card'));
      expect(onOpenModal).toHaveBeenCalledWith('cat-tracker');
    });
  });

  // ── Scenario 5: Count updates after selection change ─────────────────────────

  describe('Scenario 5 — count badge updates when toolActionIds prop changes', () => {
    it('shows_updated_count_when_toolActionIds_prop_is_changed', () => {
      const { rerender, props } = renderSection({ toolActionIds: ['tt-read', 'tt-create'] });
      rerender(
        <AgentToolSummarySection {...props} toolActionIds={['tt-read']} />
      );
      const badge = screen.getByTestId('selection-count');
      expect(badge).toHaveTextContent('1');
      expect(badge).toHaveTextContent('4');
    });
  });

  // ── Scenario 6: Empty state ───────────────────────────────────────────────────

  describe('Scenario 6 — empty state when no tools selected', () => {
    it('shows_empty_state_prompt_when_toolActionIds_is_empty', () => {
      renderSection({ toolActionIds: [] });
      expect(
        screen.getByText(/no tools selected/i)
      ).toBeInTheDocument();
    });

    it('does_not_render_any_summary_cards_when_toolActionIds_is_empty', () => {
      renderSection({ toolActionIds: [] });
      expect(screen.queryAllByTestId('tool-summary-card')).toHaveLength(0);
    });
  });

  // ── Scenario 8: MCP server card shows connection status ──────────────────────

  describe('Scenario 8 — MCP server card shows connection status badge', () => {
    it('renders_connection_status_badge_on_mcp_source_card', () => {
      renderSection({ toolActionIds: ['fig-read'] });
      // The Figma Tools card should show "Failed" connection status
      expect(screen.getByText(/failed/i)).toBeInTheDocument();
    });
  });

  // ── "Add Tools" button always visible ────────────────────────────────────────

  describe('Add Tools button', () => {
    it('renders_add_tools_button_when_no_tools_selected', () => {
      renderSection({ toolActionIds: [] });
      expect(screen.getByTestId('add-tools-button')).toBeInTheDocument();
    });

    it('renders_add_tools_button_alongside_existing_cards', () => {
      renderSection({ toolActionIds: ['tt-read'] });
      expect(screen.getByTestId('add-tools-button')).toBeInTheDocument();
    });

    it('calls_onOpenModal_with_null_when_add_tools_button_is_clicked', async () => {
      const user = userEvent.setup();
      const { onOpenModal } = renderSection({ toolActionIds: [] });
      await user.click(screen.getByTestId('add-tools-button'));
      expect(onOpenModal).toHaveBeenCalledWith(null);
    });
  });

  // ── Scenario 1 (MCP): MCP card appears after selection ─────────────────────────

  describe('AgentToolSummarySection — MCP card rendering (FR-005)', () => {
    it('renders_one_mcp_card_per_server_with_selected_tools', () => {
      const mcpSelections: McpToolSelection[] = [
        { mcpServerId: 'server-abc', toolNames: ['tool1', 'tool2', 'tool3'] },
      ];
      renderSection({
        mcpServers: [alphaServer, betaServer],
        mcpSelections,
      });
      const cards = screen.queryAllByTestId('mcp-tool-source-card');
      expect(cards).toHaveLength(1);
      expect(screen.getByText('Alpha Server')).toBeInTheDocument();
    });

    it('renders_two_mcp_cards_for_two_servers', () => {
      const mcpSelections: McpToolSelection[] = [
        { mcpServerId: 'server-abc', toolNames: ['t1'] },
        { mcpServerId: 'server-xyz', toolNames: ['t2', 't3'] },
      ];
      renderSection({
        mcpServers: [alphaServer, betaServer],
        mcpSelections,
      });
      const cards = screen.queryAllByTestId('mcp-tool-source-card');
      expect(cards).toHaveLength(2);
    });

    it('does_not_render_mcp_card_for_server_with_zero_tools', () => {
      const mcpSelections: McpToolSelection[] = [
        { mcpServerId: 'server-abc', toolNames: [] },
      ];
      renderSection({
        mcpServers: [alphaServer, betaServer],
        mcpSelections,
      });
      const cards = screen.queryAllByTestId('mcp-tool-source-card');
      expect(cards).toHaveLength(0);
    });

    it('renders_empty_state_when_no_native_tools_and_no_mcp_selections', () => {
      renderSection({
        toolActionIds: [],
        mcpServers: [alphaServer, betaServer],
        mcpSelections: [],
      });
      expect(screen.getByText(/no tools selected/i)).toBeInTheDocument();
    });

    it('does_not_show_empty_state_when_mcp_card_is_present', () => {
      const mcpSelections: McpToolSelection[] = [
        { mcpServerId: 'server-abc', toolNames: ['tool1'] },
      ];
      renderSection({
        toolActionIds: [],
        mcpServers: [alphaServer, betaServer],
        mcpSelections,
      });
      expect(screen.queryByText(/no tools selected/i)).not.toBeInTheDocument();
    });

    it('calls_onRemoveMcpServer_with_correct_serverId_when_X_clicked_on_mcp_card', async () => {
      const user = userEvent.setup();
      const mcpSelections: McpToolSelection[] = [
        { mcpServerId: 'server-abc', toolNames: ['tool1'] },
      ];
      const onRemoveMcpServer = vi.fn();
      renderSection({
        mcpServers: [alphaServer, betaServer],
        mcpSelections,
        onRemoveMcpServer,
      });
      const card = screen.getByTestId('mcp-tool-source-card');
      const removeButton = within(card).queryByTestId('remove-mcp-button') || 
                           within(card).queryByRole('button', { name: /remove|delete|x|close/i });
      expect(removeButton).toBeInTheDocument();
      await user.click(removeButton!);
      expect(onRemoveMcpServer).toHaveBeenCalledWith('server-abc');
    });

    it('calls_onOpenModal_with_serverId_when_mcp_card_body_is_clicked', async () => {
      const user = userEvent.setup();
      const mcpSelections: McpToolSelection[] = [
        { mcpServerId: 'server-abc', toolNames: ['tool1'] },
      ];
      const onOpenModal = vi.fn();
      renderSection({
        mcpServers: [alphaServer, betaServer],
        mcpSelections,
        onOpenModal,
      });
      const card = screen.getByTestId('mcp-tool-source-card');
      await user.click(card);
      expect(onOpenModal).toHaveBeenCalledWith('server-abc');
    });
  });
});
