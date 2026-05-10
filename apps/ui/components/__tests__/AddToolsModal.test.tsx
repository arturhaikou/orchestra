import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import AddToolsModal, { AddToolsModalProps } from '../agents/AddToolsModal';
import { ToolCatalogueEntry, McpServer } from '../../types';
import * as useToolPickerMcpServersModule from '../../hooks/useToolPickerMcpServers';
import * as useAgentMcpAssignmentsModule from '../../hooks/useAgentMcpAssignments';

// ─── Mock useToolPickerMcpServers ─────────────────────────────────────────────
vi.mock('../../hooks/useToolPickerMcpServers', () => ({
  useToolPickerMcpServers: vi.fn(),
}));

// ─── Mock useAgentMcpAssignments ──────────────────────────────────────────────
vi.mock('../../hooks/useAgentMcpAssignments', () => ({
  useAgentMcpAssignments: vi.fn(),
}));

const mockUseToolPickerMcpServers = vi.mocked(useToolPickerMcpServersModule.useToolPickerMcpServers);
const mockUseAgentMcpAssignments = vi.mocked(useAgentMcpAssignmentsModule.useAgentMcpAssignments);

// ─── Shared test data ──────────────────────────────────────────────────────────

const mockCatalogue: ToolCatalogueEntry[] = [
  {
    actionId: 'action-read-tickets',
    actionName: 'Read Tickets',
    actionDescription: 'Read Jira tickets',
    dangerLevel: 'Safe',
    sourceId: 'cat-jira',
    sourceName: 'Jira',
    sourceType: 'native',
  },
  {
    actionId: 'action-delete-pr',
    actionName: 'Delete Pull Request',
    actionDescription: 'Permanently deletes a PR',
    dangerLevel: 'Destructive',
    sourceId: 'cat-github',
    sourceName: 'GitHub',
    sourceType: 'native',
  },
];

const mockMcpServers: McpServer[] = [
  {
    id: 'mcp-1',
    workspaceId: 'ws-test',
    name: 'My MCP Server',
    connectionStatus: 'Connected',
    transportType: 'HTTP',
    endpointUrl: 'https://mcp.example.com',
    createdAt: '2025-01-01T00:00:00Z',
  },
];

// ─── Render helper ─────────────────────────────────────────────────────────────

function renderModal(overrides: Partial<AddToolsModalProps> = {}) {
  const onCommit = vi.fn();
  const onDiscard = vi.fn();

  const props: AddToolsModalProps = {
    isOpen: true,
    initialToolActionIds: [],
    toolCatalogue: mockCatalogue,
    workspaceId: 'ws-test',
    onCommit,
    onDiscard,
    ...overrides,
  };

  render(<AddToolsModal {...props} />);
  return { onCommit, onDiscard };
}

// ─── Tests ─────────────────────────────────────────────────────────────────────

describe('AddToolsModal', () => {

  beforeEach(() => {
    vi.clearAllMocks();
    mockUseToolPickerMcpServers.mockReturnValue({ servers: [], isLoading: false, hasError: false });
    mockUseAgentMcpAssignments.mockReturnValue({ assignments: {}, isLoading: false, hasError: false });
  });

  describe('Scenario 1 — Opening the modal', () => {
    it('renders_modal_when_isOpen_is_true', () => {
      renderModal({ isOpen: true });
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('does_not_render_modal_when_isOpen_is_false', () => {
      renderModal({ isOpen: false });
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('shows_add_tools_heading_in_modal_header', () => {
      renderModal();
      expect(screen.getByText('Add Tools')).toBeInTheDocument();
    });

    it('shows_welcome_instruction_screen_on_initial_open', () => {
      renderModal({ initialToolActionIds: [] });
      expect(
        screen.getByText(/select a category or mcp server/i)
      ).toBeInTheDocument();
    });

    it('renders_search_input_in_modal_header', () => {
      renderModal();
      expect(
        screen.getByPlaceholderText(/search categories and tools/i)
      ).toBeInTheDocument();
    });

    it('renders_done_button_in_footer', () => {
      renderModal();
      expect(screen.getByRole('button', { name: /done/i })).toBeInTheDocument();
    });

    it('renders_cancel_link_in_footer', () => {
      renderModal();
      expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
    });
  });

  describe('Scenario 2 — Closing with ✕ discards changes', () => {
    it('calls_onDiscard_when_close_button_is_clicked', async () => {
      const user = userEvent.setup();
      const { onDiscard } = renderModal();

      await user.click(screen.getByRole('button', { name: /close/i }));

      expect(onDiscard).toHaveBeenCalledOnce();
    });

    it('does_not_call_onCommit_when_close_button_is_clicked', async () => {
      const user = userEvent.setup();
      const { onCommit } = renderModal();

      await user.click(screen.getByRole('button', { name: /close/i }));

      expect(onCommit).not.toHaveBeenCalled();
    });

    it('closes_modal_on_escape_key_press', async () => {
      const { onDiscard } = renderModal();

      fireEvent.keyDown(document, { key: 'Escape', code: 'Escape' });

      expect(onDiscard).toHaveBeenCalledOnce();
    });

    it('does_not_call_onCommit_on_escape_key_press', async () => {
      const { onCommit } = renderModal();

      fireEvent.keyDown(document, { key: 'Escape', code: 'Escape' });

      expect(onCommit).not.toHaveBeenCalled();
    });
  });

  describe('Scenario 3 — "Done" applies selections', () => {
    it('calls_onCommit_when_done_button_is_clicked', async () => {
      const user = userEvent.setup();
      const { onCommit } = renderModal({ initialToolActionIds: ['action-read-tickets'] });

      await user.click(screen.getByRole('button', { name: /done/i }));

      expect(onCommit).toHaveBeenCalledOnce();
    });

    it('passes_current_working_selections_to_onCommit', async () => {
      const user = userEvent.setup();
      const { onCommit } = renderModal({
        initialToolActionIds: ['action-read-tickets'],
      });

      await user.click(screen.getByRole('button', { name: /done/i }));

      expect(onCommit).toHaveBeenCalledWith(
        expect.arrayContaining(['action-read-tickets']),
        expect.any(Array)
      );
    });

    it('does_not_call_onDiscard_when_done_is_clicked', async () => {
      const user = userEvent.setup();
      const { onDiscard } = renderModal();

      await user.click(screen.getByRole('button', { name: /done/i }));

      expect(onDiscard).not.toHaveBeenCalled();
    });
  });

  describe('Scenario 4 — "Add Tools" button disabled state', () => {
    it('renders_modal_header_close_button_as_accessible_button', () => {
      renderModal();
      const closeBtn = screen.getByRole('button', { name: /close/i });
      expect(closeBtn).toBeInTheDocument();
    });

    it('renders_done_button_as_enabled_by_default', () => {
      renderModal();
      expect(screen.getByRole('button', { name: /done/i })).not.toBeDisabled();
    });
  });

  describe('Scenario 5 — "Cancel" discards changes', () => {
    it('calls_onDiscard_when_cancel_is_clicked', async () => {
      const user = userEvent.setup();
      const { onDiscard } = renderModal();

      await user.click(screen.getByRole('button', { name: /cancel/i }));

      expect(onDiscard).toHaveBeenCalledOnce();
    });

    it('does_not_call_onCommit_when_cancel_is_clicked', async () => {
      const user = userEvent.setup();
      const { onCommit } = renderModal();

      await user.click(screen.getByRole('button', { name: /cancel/i }));

      expect(onCommit).not.toHaveBeenCalled();
    });
  });

  describe('Accessibility', () => {
    it('modal_has_dialog_role', () => {
      renderModal();
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('modal_has_aria_modal_true', () => {
      renderModal();
      expect(screen.getByRole('dialog')).toHaveAttribute('aria-modal', 'true');
    });

    it('modal_has_accessible_label', () => {
      renderModal();
      const dialog = screen.getByRole('dialog');
      expect(
        dialog.hasAttribute('aria-label') || dialog.hasAttribute('aria-labelledby')
      ).toBe(true);
    });
  });

  describe('Scenario 2 — fetches MCP servers via hook on modal open', () => {
    beforeEach(() => {
      mockUseToolPickerMcpServers.mockReturnValue({
        servers: mockMcpServers,
        isLoading: false,
        hasError: false,
      });
    });

    it('invokes_useToolPickerMcpServers_with_workspaceId_and_isOpen', () => {
      renderModal({ isOpen: true, workspaceId: 'ws-test' });
      expect(mockUseToolPickerMcpServers).toHaveBeenCalledWith('ws-test', true);
    });

    it('renders_mcp_server_name_from_hook_result', () => {
      renderModal();
      expect(screen.getByText('My MCP Server')).toBeInTheDocument();
    });

    it('shows_mcp_loading_indicator_when_hook_returns_isLoading_true', () => {
      mockUseToolPickerMcpServers.mockReturnValue({
        servers: [],
        isLoading: true,
        hasError: false,
      });
      renderModal();
      expect(screen.getByTestId('mcp-section-loading')).toBeInTheDocument();
    });

    it('shows_mcp_empty_state_when_hook_returns_no_servers', () => {
      mockUseToolPickerMcpServers.mockReturnValue({
        servers: [],
        isLoading: false,
        hasError: false,
      });
      renderModal();
      expect(screen.getByTestId('mcp-empty-state')).toBeInTheDocument();
    });
  });

  describe('FR-005 — Scenario 4: openAtSource pre-focuses left panel', () => {
    it('highlights_the_specified_source_in_left_panel_when_openAtSource_is_set', () => {
      renderModal({ openAtSource: 'cat-jira' });
      // ToolPickerLeftPanel renders category cards with data-testid="category-card"
      // and aria-current="true" on the active one
      const jiraItem = screen.getAllByTestId('category-card').find(el =>
        el.textContent?.includes('Jira')
      );
      expect(jiraItem).toHaveAttribute('aria-current', 'true');
    });

    it('shows_no_pre_selected_source_when_openAtSource_is_null', () => {
      renderModal({ openAtSource: null });
      const items = screen.queryAllByTestId('category-card');
      for (const item of items) {
        expect(item).not.toHaveAttribute('aria-current', 'true');
      }
    });

    it('shows_no_pre_selected_source_when_openAtSource_is_omitted', () => {
      renderModal({});
      const items = screen.queryAllByTestId('category-card');
      for (const item of items) {
        expect(item).not.toHaveAttribute('aria-current', 'true');
      }
    });
  });
});

// ---------------------------------------------------------------------------
// FR-004 additions — bulk action footer tests
// ---------------------------------------------------------------------------

const fr004Catalogue: ToolCatalogueEntry[] = [
  {
    actionId: 'act-read',
    actionName: 'Read PR',
    actionDescription: 'Reads a PR',
    dangerLevel: 'Safe',
    sourceId: 'cat-cr',
    sourceName: 'Code Review',
    sourceType: 'native',
  },
  {
    actionId: 'act-comment',
    actionName: 'Post Comment',
    actionDescription: 'Posts a comment',
    dangerLevel: 'Moderate',
    sourceId: 'cat-cr',
    sourceName: 'Code Review',
    sourceType: 'native',
  },
  {
    actionId: 'act-force',
    actionName: 'Force Merge',
    actionDescription: 'Force merges',
    dangerLevel: 'Destructive',
    sourceId: 'cat-cr',
    sourceName: 'Code Review',
    sourceType: 'native',
  },
];

async function openModalAtSource(
  initialToolActionIds: string[] = [],
  overrides: Record<string, unknown> = {}
) {
  const user = userEvent.setup();
  const onCommit = vi.fn();
  const onDiscard = vi.fn();

  mockUseToolPickerMcpServers.mockReturnValue({ servers: [], isLoading: false, hasError: false });
  mockUseAgentMcpAssignments.mockReturnValue({ assignments: {}, isLoading: false, hasError: false });

  render(
    <AddToolsModal
      isOpen={true}
      initialToolActionIds={initialToolActionIds}
      toolCatalogue={fr004Catalogue}
      workspaceId="ws-test"
      onCommit={onCommit}
      onDiscard={onDiscard}
      {...(overrides as Partial<AddToolsModalProps>)}
    />
  );

  const codeReviewItem = await screen.findByText('Code Review');
  await user.click(codeReviewItem);

  return { user, onCommit, onDiscard };
}

describe('AddToolsModal — bulk select (Scenario 4)', () => {
  it('shows a "Select All" button when an active source is selected', async () => {
    await openModalAtSource([]);
    expect(screen.getByRole('button', { name: /^select all$/i })).toBeInTheDocument();
  });

  it('clicking "Select All" checks all non-destructive tools', async () => {
    const onCommit2 = vi.fn();
    mockUseToolPickerMcpServers.mockReturnValue({ servers: [], isLoading: false, hasError: false });
    mockUseAgentMcpAssignments.mockReturnValue({ assignments: {}, isLoading: false, hasError: false });
    const { unmount } = render(
      <AddToolsModal
        isOpen={true}
        initialToolActionIds={[]}
        toolCatalogue={fr004Catalogue}
        workspaceId="ws-test"
        onCommit={onCommit2}
        onDiscard={vi.fn()}
      />
    );
    const user2 = userEvent.setup();
    await user2.click(await screen.findByText('Code Review'));
    await user2.click(screen.getByRole('button', { name: /^select all$/i }));
    await user2.click(screen.getByRole('button', { name: /done/i }));
    expect(onCommit2).toHaveBeenCalledWith(
      expect.arrayContaining(['act-read', 'act-comment']),
      expect.any(Array)
    );
    expect(onCommit2.mock.calls[0][0]).not.toContain('act-force');
    unmount();
  });

  it('"Select All" button is disabled when no source is active', () => {
    mockUseToolPickerMcpServers.mockReturnValue({ servers: [], isLoading: false, hasError: false });
    mockUseAgentMcpAssignments.mockReturnValue({ assignments: {}, isLoading: false, hasError: false });
    render(
      <AddToolsModal
        isOpen={true}
        initialToolActionIds={[]}
        toolCatalogue={fr004Catalogue}
        workspaceId="ws-test"
        onCommit={vi.fn()}
        onDiscard={vi.fn()}
      />
    );
    expect(screen.getByRole('button', { name: /^select all$/i })).toBeDisabled();
  });
});

describe('AddToolsModal — bulk deselect (Scenario 5)', () => {
  it('clicking "Deselect All" unchecks all tools in the current source', async () => {
    const onCommit = vi.fn();
    mockUseToolPickerMcpServers.mockReturnValue({ servers: [], isLoading: false, hasError: false });
    mockUseAgentMcpAssignments.mockReturnValue({ assignments: {}, isLoading: false, hasError: false });
    render(
      <AddToolsModal
        isOpen={true}
        initialToolActionIds={['act-read', 'act-comment']}
        toolCatalogue={fr004Catalogue}
        workspaceId="ws-test"
        onCommit={onCommit}
        onDiscard={vi.fn()}
      />
    );
    const user = userEvent.setup();
    await user.click(await screen.findByText('Code Review'));
    await user.click(screen.getByRole('button', { name: /deselect all/i }));
    await user.click(screen.getByRole('button', { name: /done/i }));
    expect(onCommit).toHaveBeenCalledWith(
      expect.not.arrayContaining(['act-read', 'act-comment']),
      expect.any(Array)
    );
  });

  it('"Deselect All" button is disabled when no tools are checked in the source', async () => {
    await openModalAtSource([]);
    expect(screen.getByRole('button', { name: /deselect all/i })).toBeDisabled();
  });
});

describe('AddToolsModal — indeterminate bulk state (Scenario 6)', () => {
  it('bulk control shows indeterminate state when some (not all) enabled tools are checked', async () => {
    await openModalAtSource(['act-read']);
    const selectAllBtn = screen.getByRole('button', { name: /^select all$/i });
    expect(selectAllBtn.closest('[data-bulk-state]')).toHaveAttribute(
      'data-bulk-state',
      'indeterminate'
    );
  });

  it('bulk control shows "all" state when all enabled tools are selected', async () => {
    await openModalAtSource(['act-read', 'act-comment']);
    const selectAllBtn = screen.getByRole('button', { name: /^select all$/i });
    expect(selectAllBtn.closest('[data-bulk-state]')).toHaveAttribute(
      'data-bulk-state',
      'all'
    );
  });

  it('bulk control shows "none" state when no tools are selected', async () => {
    await openModalAtSource([]);
    const selectAllBtn = screen.getByRole('button', { name: /^select all$/i });
    expect(selectAllBtn.closest('[data-bulk-state]')).toHaveAttribute(
      'data-bulk-state',
      'none'
    );
  });
});

// ---------------------------------------------------------------------------
// FR-004 — Destructive Tool Confirmation Dialog Integration Tests
// ---------------------------------------------------------------------------

const fr004DestructiveCatalogue: ToolCatalogueEntry[] = [
  {
    actionId: 'action-create-pr',
    actionName: 'Create PR',
    actionDescription: 'Creates a pull request',
    dangerLevel: 'Safe',
    sourceId: 'cat-github',
    sourceName: 'GitHub',
    sourceType: 'native',
  },
  {
    actionId: 'action-comment-pr',
    actionName: 'Comment on PR',
    actionDescription: 'Posts a comment on a PR',
    dangerLevel: 'Moderate',
    sourceId: 'cat-github',
    sourceName: 'GitHub',
    sourceType: 'native',
  },
  {
    actionId: 'action-delete-pr',
    actionName: 'Delete Pull Request',
    actionDescription: 'Permanently deletes a PR',
    dangerLevel: 'Destructive',
    sourceId: 'cat-github',
    sourceName: 'GitHub',
    sourceType: 'native',
  },
  {
    actionId: 'action-force-push',
    actionName: 'Force Push',
    actionDescription: 'Force pushes to repository',
    dangerLevel: 'Destructive',
    sourceId: 'cat-github',
    sourceName: 'GitHub',
    sourceType: 'native',
  },
];

async function openDestructiveModalAtGitHub(
  initialToolActionIds: string[] = [],
  overrides: Record<string, unknown> = {}
) {
  const user = userEvent.setup();
  const onCommit = vi.fn();
  const onDiscard = vi.fn();

  mockUseToolPickerMcpServers.mockReturnValue({ servers: [], isLoading: false, hasError: false });
  mockUseAgentMcpAssignments.mockReturnValue({ assignments: {}, isLoading: false, hasError: false });

  render(
    <AddToolsModal
      isOpen={true}
      initialToolActionIds={initialToolActionIds}
      toolCatalogue={fr004DestructiveCatalogue}
      workspaceId="ws-test"
      onCommit={onCommit}
      onDiscard={onDiscard}
      {...(overrides as Partial<AddToolsModalProps>)}
    />
  );

  const githubItem = await screen.findByText('GitHub');
  await user.click(githubItem);

  return { user, onCommit, onDiscard };
}

describe('AddToolsModal — FR-004 Destructive Tool Confirmation Dialog', () => {
  describe('Scenario 1: Single Destructive Tool Click Shows Dialog', () => {
    it('clicking_a_destructive_tool_checkbox_shows_the_confirmation_dialog', async () => {
      const { user } = await openDestructiveModalAtGitHub([]);

      const deletePrCheckbox = screen.getByRole('checkbox', { name: /delete pull request/i });
      await user.click(deletePrCheckbox);

      expect(
        screen.getByText(/this tool can perform irreversible or high-impact actions/i)
      ).toBeInTheDocument();
    });

    it('confirmation_dialog_contains_confirm_button', async () => {
      const { user } = await openDestructiveModalAtGitHub([]);

      const deletePrCheckbox = screen.getByRole('checkbox', { name: /delete pull request/i });
      await user.click(deletePrCheckbox);

      expect(screen.getByRole('button', { name: /confirm/i })).toBeInTheDocument();
    });

    it('confirmation_dialog_contains_cancel_button', async () => {
      const { user } = await openDestructiveModalAtGitHub([]);

      const deletePrCheckbox = screen.getByRole('checkbox', { name: /delete pull request/i });
      await user.click(deletePrCheckbox);

      expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
    });

    it('destructive_tool_checkbox_is_not_checked_before_confirmation', async () => {
      const { user } = await openDestructiveModalAtGitHub([]);

      const deletePrCheckbox = screen.getByRole('checkbox', {
        name: /delete pull request/i,
      }) as HTMLInputElement;
      await user.click(deletePrCheckbox);

      expect(deletePrCheckbox.checked).toBe(false);
    });
  });

  describe('Scenario 2: User Confirms — Tool Is Selected', () => {
    it('clicking_confirm_on_the_dialog_adds_the_destructive_tool_to_the_selection', async () => {
      const { user, onCommit } = await openDestructiveModalAtGitHub([]);

      const deletePrCheckbox = screen.getByRole('checkbox', { name: /delete pull request/i });
      await user.click(deletePrCheckbox);

      const confirmButton = screen.getByRole('button', { name: /confirm/i });
      await user.click(confirmButton);

      await user.click(screen.getByRole('button', { name: /done/i }));

      expect(onCommit).toHaveBeenCalledWith(
        expect.arrayContaining(['action-delete-pr']),
        expect.any(Array)
      );
    });

    it('confirmation_dialog_closes_after_confirm_click', async () => {
      const { user } = await openDestructiveModalAtGitHub([]);

      const deletePrCheckbox = screen.getByRole('checkbox', { name: /delete pull request/i });
      await user.click(deletePrCheckbox);

      const confirmButton = screen.getByRole('button', { name: /confirm/i });
      await user.click(confirmButton);

      expect(
        screen.queryByText(/this tool can perform irreversible or high-impact actions/i)
      ).not.toBeInTheDocument();
    });

    it('after_confirming_the_destructive_tool_checkbox_appears_checked', async () => {
      const { user } = await openDestructiveModalAtGitHub([]);

      const deletePrCheckbox = screen.getByRole('checkbox', {
        name: /delete pull request/i,
      }) as HTMLInputElement;
      await user.click(deletePrCheckbox);

      const confirmButton = screen.getByRole('button', { name: /confirm/i });
      await user.click(confirmButton);

      expect(deletePrCheckbox.checked).toBe(true);
    });
  });

  describe('Scenario 3: User Cancels — Tool Remains Unselected', () => {
    it('clicking_cancel_on_the_dialog_closes_it_without_selecting_the_tool', async () => {
      const { user } = await openDestructiveModalAtGitHub([]);

      const deletePrCheckbox = screen.getByRole('checkbox', { name: /delete pull request/i });
      await user.click(deletePrCheckbox);

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      expect(
        screen.queryByText(/this tool can perform irreversible or high-impact actions/i)
      ).not.toBeInTheDocument();
    });

    it('destructive_tool_checkbox_remains_unchecked_after_cancel', async () => {
      const { user } = await openDestructiveModalAtGitHub([]);

      const deletePrCheckbox = screen.getByRole('checkbox', {
        name: /delete pull request/i,
      }) as HTMLInputElement;
      await user.click(deletePrCheckbox);

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      expect(deletePrCheckbox.checked).toBe(false);
    });

    it('cancelling_does_not_add_destructive_tool_to_selection', async () => {
      const { user, onCommit } = await openDestructiveModalAtGitHub([]);

      const deletePrCheckbox = screen.getByRole('checkbox', { name: /delete pull request/i });
      await user.click(deletePrCheckbox);

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      await user.click(screen.getByRole('button', { name: /done/i }));

      expect(onCommit).not.toHaveBeenCalledWith(
        expect.arrayContaining(['action-delete-pr']),
        expect.any(Array)
      );
    });

    it('cancelling_does_not_affect_other_already_selected_tools', async () => {
      const { user, onCommit } = await openDestructiveModalAtGitHub(['action-create-pr']);

      const deletePrCheckbox = screen.getByRole('checkbox', { name: /delete pull request/i });
      await user.click(deletePrCheckbox);

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      await user.click(screen.getByRole('button', { name: /done/i }));

      expect(onCommit).toHaveBeenCalledWith(
        expect.arrayContaining(['action-create-pr']),
        expect.any(Array)
      );
      expect(onCommit.mock.calls[0][0]).not.toContain('action-delete-pr');
    });
  });

  describe('Scenario 4: Select All with Destructive Tools', () => {
    it('select_all_immediately_selects_safe_tools_without_showing_dialog', async () => {
      const { user } = await openDestructiveModalAtGitHub([]);

      const selectAllButton = screen.getByRole('button', { name: /^select all$/i });
      await user.click(selectAllButton);

      const createPrCheckbox = screen.getByRole('checkbox', {
        name: /create pr/i,
      }) as HTMLInputElement;

      expect(createPrCheckbox.checked).toBe(true);
    });

    it('select_all_immediately_selects_moderate_tools_without_showing_dialog', async () => {
      const { user } = await openDestructiveModalAtGitHub([]);

      const selectAllButton = screen.getByRole('button', { name: /^select all$/i });
      await user.click(selectAllButton);

      const commentCheckbox = screen.getByRole('checkbox', {
        name: /comment on pr/i,
      }) as HTMLInputElement;

      expect(commentCheckbox.checked).toBe(true);
    });

    it('select_all_shows_exactly_one_dialog_even_with_multiple_destructive_tools', async () => {
      const { user } = await openDestructiveModalAtGitHub([]);

      const selectAllButton = screen.getByRole('button', { name: /^select all$/i });
      await user.click(selectAllButton);

      const dialogTexts = screen.queryAllByText(
        /this tool can perform irreversible or high-impact actions/i
      );
      expect(dialogTexts).toHaveLength(1);
    });

    it('confirming_select_all_dialog_adds_all_destructive_tools_to_selection', async () => {
      const { user, onCommit } = await openDestructiveModalAtGitHub([]);

      const selectAllButton = screen.getByRole('button', { name: /^select all$/i });
      await user.click(selectAllButton);

      const confirmButton = screen.getByRole('button', { name: /confirm/i });
      await user.click(confirmButton);

      await user.click(screen.getByRole('button', { name: /done/i }));

      expect(onCommit).toHaveBeenCalledWith(
        expect.arrayContaining(['action-delete-pr', 'action-force-push']),
        expect.any(Array)
      );
    });

    it('confirming_select_all_also_includes_safe_and_moderate_tools', async () => {
      const { user, onCommit } = await openDestructiveModalAtGitHub([]);

      const selectAllButton = screen.getByRole('button', { name: /^select all$/i });
      await user.click(selectAllButton);

      const confirmButton = screen.getByRole('button', { name: /confirm/i });
      await user.click(confirmButton);

      await user.click(screen.getByRole('button', { name: /done/i }));

      expect(onCommit).toHaveBeenCalledWith(
        expect.arrayContaining(['action-create-pr', 'action-comment-pr', 'action-delete-pr', 'action-force-push']),
        expect.any(Array)
      );
    });

    it('cancelling_select_all_dialog_leaves_destructive_tools_unselected', async () => {
      const { user, onCommit } = await openDestructiveModalAtGitHub([]);

      const selectAllButton = screen.getByRole('button', { name: /^select all$/i });
      await user.click(selectAllButton);

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      await user.click(screen.getByRole('button', { name: /done/i }));

      expect(onCommit).not.toHaveBeenCalledWith(
        expect.arrayContaining(['action-delete-pr']),
        expect.any(Array)
      );
      expect(onCommit).not.toHaveBeenCalledWith(
        expect.arrayContaining(['action-force-push']),
        expect.any(Array)
      );
    });

    it('cancelling_select_all_dialog_still_includes_safe_and_moderate_tools', async () => {
      const { user, onCommit } = await openDestructiveModalAtGitHub([]);

      const selectAllButton = screen.getByRole('button', { name: /^select all$/i });
      await user.click(selectAllButton);

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      await user.click(screen.getByRole('button', { name: /done/i }));

      expect(onCommit).toHaveBeenCalledWith(
        expect.arrayContaining(['action-create-pr', 'action-comment-pr']),
        expect.any(Array)
      );
      expect(onCommit.mock.calls[0][0]).not.toContain('action-delete-pr');
      expect(onCommit.mock.calls[0][0]).not.toContain('action-force-push');
    });

    it('dialog_closes_after_confirm_in_select_all_scenario', async () => {
      const { user } = await openDestructiveModalAtGitHub([]);

      const selectAllButton = screen.getByRole('button', { name: /^select all$/i });
      await user.click(selectAllButton);

      const confirmButton = screen.getByRole('button', { name: /confirm/i });
      await user.click(confirmButton);

      expect(
        screen.queryByText(/this tool can perform irreversible or high-impact actions/i)
      ).not.toBeInTheDocument();
    });

    it('dialog_closes_after_cancel_in_select_all_scenario', async () => {
      const { user } = await openDestructiveModalAtGitHub([]);

      const selectAllButton = screen.getByRole('button', { name: /^select all$/i });
      await user.click(selectAllButton);

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      expect(
        screen.queryByText(/this tool can perform irreversible or high-impact actions/i)
      ).not.toBeInTheDocument();
    });
  });

  describe('Edge Cases: Destructive Tool Interactions', () => {
    it('can_confirm_multiple_destructive_tools_individually', async () => {
      const { user, onCommit } = await openDestructiveModalAtGitHub([]);

      const deletePrCheckbox = screen.getByRole('checkbox', { name: /delete pull request/i });
      await user.click(deletePrCheckbox);

      let confirmButton = screen.getByRole('button', { name: /confirm/i });
      await user.click(confirmButton);

      const forcePushCheckbox = screen.getByRole('checkbox', { name: /force push/i });
      await user.click(forcePushCheckbox);

      confirmButton = screen.getByRole('button', { name: /confirm/i });
      await user.click(confirmButton);

      await user.click(screen.getByRole('button', { name: /done/i }));

      expect(onCommit).toHaveBeenCalledWith(
        expect.arrayContaining(['action-delete-pr', 'action-force-push']),
        expect.any(Array)
      );
    });

    it('cancelling_one_destructive_tool_does_not_affect_previously_confirmed_destructive_tools', async () => {
      const { user, onCommit } = await openDestructiveModalAtGitHub([]);

      const deletePrCheckbox = screen.getByRole('checkbox', { name: /delete pull request/i });
      await user.click(deletePrCheckbox);

      let confirmButton = screen.getByRole('button', { name: /confirm/i });
      await user.click(confirmButton);

      const forcePushCheckbox = screen.getByRole('checkbox', { name: /force push/i });
      await user.click(forcePushCheckbox);

      let cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      await user.click(screen.getByRole('button', { name: /done/i }));

      expect(onCommit).toHaveBeenCalledWith(
        expect.arrayContaining(['action-delete-pr']),
        expect.any(Array)
      );
      expect(onCommit.mock.calls[0][0]).not.toContain('action-force-push');
    });

    it('all_destructive_tools_remain_unchecked_if_select_all_is_cancelled', async () => {
      const { user } = await openDestructiveModalAtGitHub([]);

      const selectAllButton = screen.getByRole('button', { name: /^select all$/i });
      await user.click(selectAllButton);

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      const deletePrCheckbox = screen.getByRole('checkbox', {
        name: /delete pull request/i,
      }) as HTMLInputElement;
      const forcePushCheckbox = screen.getByRole('checkbox', {
        name: /force push/i,
      }) as HTMLInputElement;

      expect(deletePrCheckbox.checked).toBe(false);
      expect(forcePushCheckbox.checked).toBe(false);
    });

    it('all_destructive_tools_are_checked_if_select_all_is_confirmed', async () => {
      const { user } = await openDestructiveModalAtGitHub([]);

      const selectAllButton = screen.getByRole('button', { name: /^select all$/i });
      await user.click(selectAllButton);

      const confirmButton = screen.getByRole('button', { name: /confirm/i });
      await user.click(confirmButton);

      const deletePrCheckbox = screen.getByRole('checkbox', {
        name: /delete pull request/i,
      }) as HTMLInputElement;
      const forcePushCheckbox = screen.getByRole('checkbox', {
        name: /force push/i,
      }) as HTMLInputElement;

      expect(deletePrCheckbox.checked).toBe(true);
      expect(forcePushCheckbox.checked).toBe(true);
    });

    it('safe_and_moderate_tools_are_not_affected_by_destructive_tool_cancel', async () => {
      const { user } = await openDestructiveModalAtGitHub(['action-create-pr', 'action-comment-pr']);

      const selectAllButton = screen.getByRole('button', { name: /^select all$/i });
      await user.click(selectAllButton);

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      const createPrCheckbox = screen.getByRole('checkbox', {
        name: /create pr/i,
      }) as HTMLInputElement;
      const commentCheckbox = screen.getByRole('checkbox', {
        name: /comment on pr/i,
      }) as HTMLInputElement;

      expect(createPrCheckbox.checked).toBe(true);
      expect(commentCheckbox.checked).toBe(true);
    });
  });
});
