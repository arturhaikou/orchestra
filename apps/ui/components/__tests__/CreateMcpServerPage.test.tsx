import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import CreateMcpServerPage from '../pages/CreateMcpServerPage';

// ── Placeholder pages for navigation targets ──────────────────────────────────
const McpServersListPlaceholder = () => (
  <div data-testid="mcp-servers-list">MCP Servers List</div>
);

interface RenderOptions {
  workspaceId?: string;
  transportType?: 'http' | 'stdio';
  isConnectionVerified?: boolean;
}

// ── Router factory — uses createMemoryRouter for useBlocker support ───────────
const renderCreatePage = ({ workspaceId = 'ws-test', transportType, isConnectionVerified }: RenderOptions = {}) => {
  const router = createMemoryRouter(
    [
      {
        path: '/workspaces/:workspaceId/mcp-servers',
        element: <McpServersListPlaceholder />,
      },
      {
        path: '/workspaces/:workspaceId/mcp-servers/new',
        element: (
          <CreateMcpServerPage
            _initialTransportType={transportType}
            _initialConnectionVerified={isConnectionVerified}
          />
        ),
      },
    ],
    {
      initialEntries: [`/workspaces/${workspaceId}/mcp-servers/new`],
    }
  );

  return render(<RouterProvider router={router} />);
};

beforeEach(() => {
  vi.clearAllMocks();
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 1: User navigates to the create page and sees the form
// ─────────────────────────────────────────────────────────────────────────────
describe('Scenario 1: Page structure on load', () => {
  it('renders_page_heading_add_mcp_server', () => {
    renderCreatePage();

    expect(screen.getByRole('heading', { level: 1, name: /add mcp server/i })).toBeInTheDocument();
  });

  it('renders_breadcrumb_mcp_servers_link', () => {
    renderCreatePage();

    expect(screen.getByRole('link', { name: /mcp servers/i })).toBeInTheDocument();
  });

  it('renders_breadcrumb_add_mcp_server_text', () => {
    renderCreatePage();

    expect(screen.getByText('Add MCP Server', { selector: 'span' })).toBeInTheDocument();
  });

  it('renders_server_name_field', () => {
    renderCreatePage();

    expect(screen.getByLabelText(/server name/i)).toBeInTheDocument();
  });

  it('renders_transport_type_selector_defaulting_to_http', () => {
    renderCreatePage();

    const httpButton = screen.getByRole('radio', { name: /http/i });
    expect(httpButton).toHaveAttribute('aria-checked', 'true');
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 2: Switching transport type resets connection detail fields
// ─────────────────────────────────────────────────────────────────────────────
describe('Scenario 2: Transport type switch clears connection fields', () => {
  it('switching_to_stdio_removes_http_url_field', async () => {
    const user = userEvent.setup();
    renderCreatePage();

    const urlInput = screen.getByLabelText(/endpoint url/i);
    await user.type(urlInput, 'https://example.com/mcp');

    await user.click(screen.getByRole('radio', { name: /stdio/i }));

    expect(screen.queryByLabelText(/endpoint url/i)).not.toBeInTheDocument();
  });

  it('switching_to_stdio_shows_command_field', async () => {
    const user = userEvent.setup();
    renderCreatePage();

    await user.click(screen.getByRole('radio', { name: /stdio/i }));

    expect(screen.getByLabelText(/command/i)).toBeInTheDocument();
  });

  it('switching_to_stdio_clears_previously_entered_url_value', async () => {
    const user = userEvent.setup();
    renderCreatePage();

    await user.type(screen.getByLabelText(/endpoint url/i), 'https://example.com/mcp');
    await user.click(screen.getByRole('radio', { name: /stdio/i }));
    await user.click(screen.getByRole('radio', { name: /http/i }));

    expect(screen.getByLabelText<HTMLInputElement>(/endpoint url/i).value).toBe('');
  });

  it('switching_transport_preserves_server_name_field_value', async () => {
    const user = userEvent.setup();
    renderCreatePage();

    await user.type(screen.getByLabelText(/server name/i), 'My Server');
    await user.click(screen.getByRole('radio', { name: /stdio/i }));

    expect(screen.getByLabelText<HTMLInputElement>(/server name/i).value).toBe('My Server');
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 3: Cancel with no form data goes directly to list page
// ─────────────────────────────────────────────────────────────────────────────
describe('Scenario 3: Cancel with clean form navigates without dialog', () => {
  it('cancel_with_no_data_navigates_to_list_page', async () => {
    const user = userEvent.setup();
    renderCreatePage();

    await user.click(screen.getByRole('button', { name: /cancel/i }));

    await waitFor(() =>
      expect(screen.getByTestId('mcp-servers-list')).toBeInTheDocument()
    );
  });

  it('cancel_with_no_data_does_not_show_dialog', async () => {
    const user = userEvent.setup();
    renderCreatePage();

    await user.click(screen.getByRole('button', { name: /cancel/i }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 4: Cancel with unsaved form data triggers confirmation dialog
// ─────────────────────────────────────────────────────────────────────────────
describe('Scenario 4: Cancel with dirty form shows unsaved changes dialog', () => {
  it('cancel_after_typing_server_name_shows_dialog', async () => {
    const user = userEvent.setup();
    renderCreatePage();

    await user.type(screen.getByLabelText(/server name/i), 'My Server');
    await user.click(screen.getByRole('button', { name: /cancel/i }));

    await waitFor(() =>
      expect(screen.getByRole('dialog')).toBeInTheDocument()
    );
  });

  it('dialog_contains_stay_on_page_button', async () => {
    const user = userEvent.setup();
    renderCreatePage();

    await user.type(screen.getByLabelText(/server name/i), 'My Server');
    await user.click(screen.getByRole('button', { name: /cancel/i }));

    await waitFor(() => screen.getByRole('dialog'));

    expect(screen.getByRole('button', { name: /stay on page/i })).toBeInTheDocument();
  });

  it('dialog_contains_leave_without_saving_button', async () => {
    const user = userEvent.setup();
    renderCreatePage();

    await user.type(screen.getByLabelText(/server name/i), 'My Server');
    await user.click(screen.getByRole('button', { name: /cancel/i }));

    await waitFor(() => screen.getByRole('dialog'));

    expect(screen.getByRole('button', { name: /leave without saving/i })).toBeInTheDocument();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 5: User confirms leaving — navigated to list page
// ─────────────────────────────────────────────────────────────────────────────
describe('Scenario 5: Leave without saving navigates to list page', () => {
  it('clicking_leave_without_saving_navigates_to_list_page', async () => {
    const user = userEvent.setup();
    renderCreatePage();

    await user.type(screen.getByLabelText(/server name/i), 'My Server');
    await user.click(screen.getByRole('button', { name: /cancel/i }));

    await waitFor(() => screen.getByRole('dialog'));

    await user.click(screen.getByRole('button', { name: /leave without saving/i }));

    await waitFor(() =>
      expect(screen.getByTestId('mcp-servers-list')).toBeInTheDocument()
    );
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 6: User chooses to stay — form is preserved
// ─────────────────────────────────────────────────────────────────────────────
describe('Scenario 6: Stay on page preserves form values', () => {
  it('clicking_stay_on_page_closes_dialog', async () => {
    const user = userEvent.setup();
    renderCreatePage();

    await user.type(screen.getByLabelText(/server name/i), 'My Server');
    await user.click(screen.getByRole('button', { name: /cancel/i }));

    await waitFor(() => screen.getByRole('dialog'));

    await user.click(screen.getByRole('button', { name: /stay on page/i }));

    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    );
  });

  it('clicking_stay_on_page_keeps_user_on_create_page', async () => {
    const user = userEvent.setup();
    renderCreatePage();

    await user.type(screen.getByLabelText(/server name/i), 'My Server');
    await user.click(screen.getByRole('button', { name: /cancel/i }));

    await waitFor(() => screen.getByRole('dialog'));

    await user.click(screen.getByRole('button', { name: /stay on page/i }));

    expect(screen.getByRole('heading', { level: 1, name: /add mcp server/i })).toBeInTheDocument();
  });

  it('clicking_stay_on_page_preserves_previously_typed_server_name', async () => {
    const user = userEvent.setup();
    renderCreatePage();

    await user.type(screen.getByLabelText(/server name/i), 'My Server');
    await user.click(screen.getByRole('button', { name: /cancel/i }));

    await waitFor(() => screen.getByRole('dialog'));

    await user.click(screen.getByRole('button', { name: /stay on page/i }));

    expect(screen.getByLabelText<HTMLInputElement>(/server name/i).value).toBe('My Server');
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 7: Save button is disabled on initial page load
// ─────────────────────────────────────────────────────────────────────────────
describe('Scenario 7: Save button is disabled on initial load', () => {
  it('save_button_is_disabled_on_page_load', () => {
    renderCreatePage();

    const saveButton = screen.getByRole('button', { name: /save mcp server/i });
    expect(saveButton).toBeDisabled();
  });

  it('save_button_has_tooltip_about_connection_verification', () => {
    renderCreatePage();

    const saveButton = screen.getByRole('button', { name: /save mcp server/i });
    expect(saveButton).toHaveAttribute('title', 'Please verify the connection first');
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 8: Browser back button with unsaved data triggers confirmation dialog
// ─────────────────────────────────────────────────────────────────────────────
describe('Scenario 8: Browser back navigation with dirty form shows dialog', () => {
  it('browser_back_with_dirty_form_shows_unsaved_changes_dialog', async () => {
    const user = userEvent.setup();

    const router = createMemoryRouter(
      [
        {
          path: '/workspaces/:workspaceId/mcp-servers',
          element: <McpServersListPlaceholder />,
        },
        {
          path: '/workspaces/:workspaceId/mcp-servers/new',
          element: <CreateMcpServerPage />,
        },
      ],
      {
        initialEntries: [
          '/workspaces/ws-test/mcp-servers',
          '/workspaces/ws-test/mcp-servers/new',
        ],
        initialIndex: 1,
      }
    );

    render(<RouterProvider router={router} />);

    await user.type(screen.getByLabelText(/server name/i), 'My Server');

    router.navigate(-1);

    await waitFor(() =>
      expect(screen.getByRole('dialog')).toBeInTheDocument()
    );
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 9: Breadcrumb "MCP Servers" link navigates to list page
// ─────────────────────────────────────────────────────────────────────────────
describe('Scenario 9: Breadcrumb link returns to list page', () => {
  it('clicking_mcp_servers_breadcrumb_navigates_to_list_page', async () => {
    const user = userEvent.setup();
    renderCreatePage();

    await user.click(screen.getByRole('link', { name: /mcp servers/i }));

    await waitFor(() =>
      expect(screen.getByTestId('mcp-servers-list')).toBeInTheDocument()
    );
  });

  it('clicking_breadcrumb_with_dirty_form_shows_unsaved_changes_dialog', async () => {
    const user = userEvent.setup();
    renderCreatePage();

    await user.type(screen.getByLabelText(/server name/i), 'My Server');

    await user.click(screen.getByRole('link', { name: /mcp servers/i }));

    await waitFor(() =>
      expect(screen.getByRole('dialog')).toBeInTheDocument()
    );
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 8: Editing Command after successful connect resets verified state
// ─────────────────────────────────────────────────────────────────────────────

describe('Scenario 8: Editing Command field after successful connect disables Save', () => {
  it('disables_save_button_when_command_changes_after_connection_verified', async () => {
    renderCreatePage({ transportType: 'stdio', isConnectionVerified: true });

    const saveButton = screen.getByRole('button', { name: /save mcp server/i });
    expect(saveButton).not.toBeDisabled();

    const commandInput = screen.getByLabelText(/command/i);
    await userEvent.clear(commandInput);
    await userEvent.type(commandInput, 'docker');

    expect(saveButton).toBeDisabled();
  });

  it('shows_reconnect_hint_when_command_changes_after_connection_verified', async () => {
    renderCreatePage({ transportType: 'stdio', isConnectionVerified: true });

    const commandInput = screen.getByLabelText(/command/i);
    await userEvent.clear(commandInput);
    await userEvent.type(commandInput, 'docker');

    expect(screen.getByText(/reconnect/i)).toBeInTheDocument();
  });
});
