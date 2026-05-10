import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect } from 'vitest';
import ToolPickerRightPanel, {
  ToolPickerRightPanelProps,
} from '../agents/ToolPickerRightPanel';
import { ToolCatalogueEntry, McpToolFetchState, McpFetchedTool } from '../../types';

// ─── Shared test data ──────────────────────────────────────────────────────────

const codeReviewTools: ToolCatalogueEntry[] = [
  {
    actionId: 'action-cr-read',
    actionName: 'Read PR',
    actionDescription: 'Reads an open pull request',
    dangerLevel: 'Safe',
    sourceId: 'cat-code-review',
    sourceName: 'Code Review',
    sourceType: 'native',
  },
  {
    actionId: 'action-cr-comment',
    actionName: 'Post Comment',
    actionDescription: 'Posts a review comment on a PR',
    dangerLevel: 'Moderate',
    sourceId: 'cat-code-review',
    sourceName: 'Code Review',
    sourceType: 'native',
  },
  {
    actionId: 'action-cr-force-merge',
    actionName: 'Force Merge',
    actionDescription: 'Bypasses branch protection and force-merges',
    dangerLevel: 'Destructive',
    sourceId: 'cat-code-review',
    sourceName: 'Code Review',
    sourceType: 'native',
  },
];

const ticketTools: ToolCatalogueEntry[] = [
  {
    actionId: 'action-ticket-read',
    actionName: 'Read Ticket',
    actionDescription: 'Reads a Jira ticket',
    dangerLevel: 'Safe',
    sourceId: 'cat-tickets',
    sourceName: 'Tickets',
    sourceType: 'native',
  },
];

const allCatalogue = [...codeReviewTools, ...ticketTools];

// ─── MCP test data ─────────────────────────────────────────────────────────

const sampleMcpTools: McpFetchedTool[] = [
  {
    name: 'list-repos',
    description: 'Lists repositories',
    dangerLevel: 'Safe',
  },
  {
    name: 'delete-repo',
    description: 'Deletes a repository',
    dangerLevel: 'Destructive',
  },
];

// ─── Render helper ─────────────────────────────────────────────────────────────

function renderPanel(overrides: Partial<ToolPickerRightPanelProps> = {}) {
  const onToggleTool = vi.fn();
  const onDestructiveToolAttempt = vi.fn();
  const props: ToolPickerRightPanelProps = {
    toolCatalogue: allCatalogue,
    activeSourceId: 'cat-code-review',
    activeSourceType: 'native',
    searchTerm: '',
    workingSelection: [],
    onToggleTool,
    onDestructiveToolAttempt,
    ...overrides,
  };
  render(<ToolPickerRightPanel {...props} />);
  return { onToggleTool, onDestructiveToolAttempt };
}

// ─── Suite 1 — Welcome screen (no active source) ──────────────────────────────

describe('ToolPickerRightPanel — welcome screen', () => {
  it('renders welcome/instruction content when activeSourceId is null', () => {
    renderPanel({ activeSourceId: null });
    expect(screen.getByText(/select a source/i)).toBeInTheDocument();
  });

  it('does not render any tool rows when activeSourceId is null', () => {
    renderPanel({ activeSourceId: null });
    expect(screen.queryAllByRole('checkbox')).toHaveLength(0);
  });
});

// ─── Suite 2 — Tool rows for active source (Scenario 1) ───────────────────────

describe('ToolPickerRightPanel — tool rows', () => {
  it('renders a row for each tool belonging to the active source', () => {
    renderPanel();
    expect(screen.getByText('Read PR')).toBeInTheDocument();
    expect(screen.getByText('Post Comment')).toBeInTheDocument();
    expect(screen.getByText('Force Merge')).toBeInTheDocument();
  });

  it('does not render tools from other sources', () => {
    renderPanel();
    expect(screen.queryByText('Read Ticket')).not.toBeInTheDocument();
  });

  it('renders tool description text', () => {
    renderPanel();
    expect(screen.getByText('Reads an open pull request')).toBeInTheDocument();
  });

  it('renders a danger level badge for each tool', () => {
    renderPanel();
    expect(screen.getByText('Safe')).toBeInTheDocument();
    expect(screen.getByText('Moderate')).toBeInTheDocument();
    expect(screen.getByText('Destructive')).toBeInTheDocument();
  });
});

// ─── Suite 3 — Panel header with selection count (Scenario 2) ─────────────────

describe('ToolPickerRightPanel — selection count header', () => {
  it('shows the source name in the panel header', () => {
    renderPanel();
    expect(screen.getByText(/code review/i)).toBeInTheDocument();
  });

  it('shows 0 selected when workingSelection is empty', () => {
    renderPanel({ workingSelection: [] });
    expect(screen.getByText(/0 selected/i)).toBeInTheDocument();
  });

  it('shows correct count when one tool is selected', () => {
    renderPanel({ workingSelection: ['action-cr-read'] });
    expect(screen.getByText(/1 selected/i)).toBeInTheDocument();
  });

  it('shows correct count when two tools are selected', () => {
    renderPanel({ workingSelection: ['action-cr-read', 'action-cr-comment'] });
    expect(screen.getByText(/2 selected/i)).toBeInTheDocument();
  });

  it('does not count tools from other sources in the selection counter', () => {
    renderPanel({ workingSelection: ['action-ticket-read'] });
    expect(screen.getByText(/0 selected/i)).toBeInTheDocument();
  });
});

// ─── Suite 4 — Destructive tool (Scenario 3) ──────────────────────────────────

describe('ToolPickerRightPanel — destructive tool', () => {
  it('renders the destructive tool checkbox as enabled (not disabled)', () => {
    renderPanel();
    const checkboxes = screen.getAllByRole('checkbox');
    const forceMergeCheckbox = checkboxes.find(
      (cb) => cb.closest('[data-testid="tool-row"]')?.textContent?.includes('Force Merge')
    );
    expect(forceMergeCheckbox).not.toBeDisabled();
  });

  it('clicking a destructive tool checkbox calls onDestructiveToolAttempt with its actionId', async () => {
    const user = userEvent.setup();
    const { onDestructiveToolAttempt, onToggleTool } = renderPanel();
    const checkboxes = screen.getAllByRole('checkbox');
    const forceMergeCheckbox = checkboxes.find(
      (cb) => cb.closest('[data-testid="tool-row"]')?.textContent?.includes('Force Merge')
    );
    await user.click(forceMergeCheckbox!);
    expect(onDestructiveToolAttempt).toHaveBeenCalledWith('action-cr-force-merge');
    expect(onToggleTool).not.toHaveBeenCalled();
  });

  it('non-destructive tool checkbox click still calls onToggleTool (unchanged behavior)', async () => {
    const user = userEvent.setup();
    const { onToggleTool, onDestructiveToolAttempt } = renderPanel();
    const checkboxes = screen.getAllByRole('checkbox');
    const readPrCheckbox = checkboxes.find(
      (cb) => cb.closest('[data-testid="tool-row"]')?.textContent?.includes('Read PR')
    );
    await user.click(readPrCheckbox!);
    expect(onToggleTool).toHaveBeenCalledWith('action-cr-read');
    expect(onDestructiveToolAttempt).not.toHaveBeenCalled();
  });

  it('renders a DangerBadge with "Destructive" label for the destructive tool', () => {
    renderPanel();
    expect(screen.getByText('Destructive')).toBeInTheDocument();
  });

  it('does not crash if onDestructiveToolAttempt prop is not provided', async () => {
    const user = userEvent.setup();
    const { onToggleTool } = renderPanel({ onDestructiveToolAttempt: undefined });
    const checkboxes = screen.getAllByRole('checkbox');
    const forceMergeCheckbox = checkboxes.find(
      (cb) => cb.closest('[data-testid="tool-row"]')?.textContent?.includes('Force Merge')
    );
    await user.click(forceMergeCheckbox!);
    expect(onToggleTool).not.toHaveBeenCalled();
  });
});

// ─── Suite 5 — Checkbox toggle interaction (Scenario 2 interaction) ───────────

describe('ToolPickerRightPanel — checkbox interaction', () => {
  it('calls onToggleTool with the correct actionId when a non-destructive checkbox is clicked', async () => {
    const user = userEvent.setup();
    const { onToggleTool } = renderPanel();
    const checkboxes = screen.getAllByRole('checkbox');
    const readPrCheckbox = checkboxes.find(
      (cb) => cb.closest('[data-testid="tool-row"]')?.textContent?.includes('Read PR')
    );
    await user.click(readPrCheckbox!);
    expect(onToggleTool).toHaveBeenCalledWith('action-cr-read');
  });

  it('clicking a destructive checkbox routes to onDestructiveToolAttempt, not onToggleTool', async () => {
    const user = userEvent.setup();
    const { onToggleTool, onDestructiveToolAttempt } = renderPanel();
    const checkboxes = screen.getAllByRole('checkbox');
    const destructiveCheckbox = checkboxes.find(
      (cb) => cb.closest('[data-testid="tool-row"]')?.textContent?.includes('Force Merge')
    );
    await user.click(destructiveCheckbox!);
    expect(onToggleTool).not.toHaveBeenCalled();
    expect(onDestructiveToolAttempt).toHaveBeenCalledWith('action-cr-force-merge');
  });

  it('renders checked checkboxes for tools in workingSelection', () => {
    renderPanel({ workingSelection: ['action-cr-read'] });
    const checkboxes = screen.getAllByRole('checkbox') as HTMLInputElement[];
    const readPrCheckbox = checkboxes.find(
      (cb) => cb.closest('[data-testid="tool-row"]')?.textContent?.includes('Read PR')
    );
    expect(readPrCheckbox?.checked).toBe(true);
  });
});

// ─── Suite 6 — Search filtering (Scenario 7) ──────────────────────────────────

describe('ToolPickerRightPanel — search filtering', () => {
  it('shows only tools whose name contains the search term (case-insensitive)', () => {
    renderPanel({ searchTerm: 'comment' });
    expect(screen.getByText('Post Comment')).toBeInTheDocument();
    expect(screen.queryByText('Read PR')).not.toBeInTheDocument();
    expect(screen.queryByText('Force Merge')).not.toBeInTheDocument();
  });

  it('matches against tool description as well as name', () => {
    renderPanel({ searchTerm: 'branch protection' });
    expect(screen.getByText('Force Merge')).toBeInTheDocument();
    expect(screen.queryByText('Read PR')).not.toBeInTheDocument();
  });

  it('is case-insensitive', () => {
    renderPanel({ searchTerm: 'READ' });
    expect(screen.getByText('Read PR')).toBeInTheDocument();
  });
});

// ─── Suite 7 — No search results (Scenario 8) ─────────────────────────────────

describe('ToolPickerRightPanel — no search results', () => {
  it('shows "No tools match" message when search term finds no tools', () => {
    renderPanel({ searchTerm: 'xyznotfound' });
    expect(screen.getByText(/no tools match/i)).toBeInTheDocument();
    expect(screen.getByText(/xyznotfound/i)).toBeInTheDocument();
  });

  it('does not render any tool rows when search has no matches', () => {
    renderPanel({ searchTerm: 'xyznotfound' });
    expect(screen.queryAllByRole('checkbox')).toHaveLength(0);
  });
});

// ─── Suite 8 — Zero tools for source (Business Rule §4) ───────────────────────

describe('ToolPickerRightPanel — zero tools for source', () => {
  it('shows "No tools are currently available" when the active source has no tools', () => {
    renderPanel({
      toolCatalogue: allCatalogue,
      activeSourceId: 'cat-empty-source',
    });
    expect(
      screen.getByText(/no tools are currently available/i)
    ).toBeInTheDocument();
  });
});

// ─── Suite 9 — MCP panel: idle state (BDD Scenario 1 pre-click) ──────────────

describe('ToolPickerRightPanel — MCP idle state', () => {
  it('shows_mcp_idle_state_when_fetch_state_is_idle', () => {
    renderPanel({
      activeSourceId: 'mcp-server-1',
      activeSourceType: 'mcp',
      mcpFetchState: { status: 'idle' },
    });
    expect(screen.getByTestId('mcp-idle-state')).toBeInTheDocument();
  });

  it('does_not_show_loading_spinner_when_fetch_state_is_idle', () => {
    renderPanel({
      activeSourceId: 'mcp-server-1',
      activeSourceType: 'mcp',
      mcpFetchState: { status: 'idle' },
    });
    expect(screen.queryByTestId('mcp-loading-state')).toBeNull();
  });

  it('idle_state_shows_invitation_text', () => {
    renderPanel({
      activeSourceId: 'mcp-server-1',
      activeSourceType: 'mcp',
      mcpFetchState: { status: 'idle' },
    });
    const idleState = screen.getByTestId('mcp-idle-state');
    expect(idleState.textContent).toMatch(/click|discover/i);
  });
});

// ─── Suite 10 — MCP panel: loading state (BDD Scenario 2 during fetch) ───────

describe('ToolPickerRightPanel — MCP loading state', () => {
  it('shows_loading_spinner_when_fetch_state_is_loading', () => {
    renderPanel({
      activeSourceId: 'mcp-server-1',
      activeSourceType: 'mcp',
      mcpFetchState: { status: 'loading' },
    });
    expect(screen.getByTestId('mcp-loading-state')).toBeInTheDocument();
  });

  it('does_not_show_idle_state_when_loading', () => {
    renderPanel({
      activeSourceId: 'mcp-server-1',
      activeSourceType: 'mcp',
      mcpFetchState: { status: 'loading' },
    });
    expect(screen.queryByTestId('mcp-idle-state')).toBeNull();
  });
});

// ─── Suite 11 — MCP panel: error state with retry (BDD Scenario 4) ───────────

describe('ToolPickerRightPanel — MCP error state', () => {
  it('shows_error_message_when_fetch_state_is_error', () => {
    renderPanel({
      activeSourceId: 'mcp-server-1',
      activeSourceType: 'mcp',
      mcpFetchState: { status: 'error', message: 'Could not reach server' },
    });
    expect(screen.getByTestId('mcp-error-state')).toBeInTheDocument();
    expect(screen.getByText(/could not reach server/i)).toBeInTheDocument();
  });

  it('shows_retry_button_in_error_state', () => {
    renderPanel({
      activeSourceId: 'mcp-server-1',
      activeSourceType: 'mcp',
      mcpFetchState: { status: 'error', message: 'Could not reach server' },
    });
    expect(screen.getByTestId('retry-fetch-button')).toBeInTheDocument();
  });

  it('calls_onRetryFetch_when_retry_button_is_clicked', async () => {
    const user = userEvent.setup();
    const onRetryFetch = vi.fn();
    renderPanel({
      activeSourceId: 'mcp-server-1',
      activeSourceType: 'mcp',
      mcpFetchState: { status: 'error', message: 'Timeout' },
      onRetryFetch,
    });
    await user.click(screen.getByTestId('retry-fetch-button'));
    expect(onRetryFetch).toHaveBeenCalledOnce();
  });

  it('does_not_show_count_badge_in_error_state', () => {
    renderPanel({
      activeSourceId: 'mcp-server-1',
      activeSourceType: 'mcp',
      mcpFetchState: { status: 'error', message: 'Timeout' },
    });
    const countBadgeElement = screen.queryByText(/selected/i);
    expect(countBadgeElement).toBeNull();
  });
});

// ─── Suite 12 — MCP panel: success state (BDD Scenario 2 after fetch) ────────

describe('ToolPickerRightPanel — MCP success state', () => {
  it('renders_mcp_tool_rows_when_fetch_state_is_success', () => {
    renderPanel({
      activeSourceId: 'mcp-server-1',
      activeSourceType: 'mcp',
      mcpFetchState: { status: 'success', tools: sampleMcpTools },
    });
    expect(screen.getByText('list-repos')).toBeInTheDocument();
    expect(screen.getAllByTestId('mcp-tool-row')).toHaveLength(2);
  });

  it('shows_selected_count_in_panel_header_for_mcp_server', () => {
    renderPanel({
      activeSourceId: 'mcp-server-1',
      activeSourceType: 'mcp',
      mcpFetchState: { status: 'success', tools: sampleMcpTools },
      workingSelection: ['mcp-server-1:list-repos'],
    });
    expect(screen.getByText(/1 selected/i)).toBeInTheDocument();
  });
});

// ─── Suite 13 — MCP destructive tool interception ──────────────────────────────

describe('ToolPickerRightPanel — MCP destructive tool interception', () => {
  it('MCP destructive tool checkbox is enabled when onDestructiveToolAttempt is provided', () => {
    renderPanel({
      activeSourceId: 'mcp-server-1',
      activeSourceType: 'mcp',
      mcpFetchState: { status: 'success', tools: sampleMcpTools },
    });
    const checkboxes = screen.getAllByRole('checkbox');
    const deleteRepoCheckbox = checkboxes.find(
      (cb) => cb.closest('[data-testid="mcp-tool-row"]')?.textContent?.includes('delete-repo')
    );
    expect(deleteRepoCheckbox).not.toBeDisabled();
  });

  it('clicking MCP destructive tool calls onDestructiveToolAttempt with composite key', async () => {
    const user = userEvent.setup();
    const { onDestructiveToolAttempt, onToggleTool } = renderPanel({
      activeSourceId: 'mcp-server-1',
      activeSourceType: 'mcp',
      mcpFetchState: { status: 'success', tools: sampleMcpTools },
    });
    const checkboxes = screen.getAllByRole('checkbox');
    const deleteRepoCheckbox = checkboxes.find(
      (cb) => cb.closest('[data-testid="mcp-tool-row"]')?.textContent?.includes('delete-repo')
    );
    await user.click(deleteRepoCheckbox!);
    expect(onDestructiveToolAttempt).toHaveBeenCalledWith('mcp-server-1:delete-repo');
    expect(onToggleTool).not.toHaveBeenCalled();
  });
});
