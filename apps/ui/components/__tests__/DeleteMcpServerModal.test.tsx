import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import DeleteMcpServerModal from '../mcp/DeleteMcpServerModal';
import { McpServer } from '../../types';

const makeServer = (overrides: Partial<McpServer> = {}): McpServer => ({
  id: 'srv-1',
  workspaceId: 'ws-1',
  name: 'My Figma Server',
  connectionStatus: 'Connected',
  transportType: 'HTTP',
  endpointUrl: 'https://mcp.figma.com/sse',
  createdAt: '2026-05-01T10:00:00.000Z',
  ...overrides,
});

const renderModal = (props: Partial<Parameters<typeof DeleteMcpServerModal>[0]> = {}) => {
  const defaults = {
    server: makeServer(),
    isDeleting: false,
    error: null,
    onCancel: vi.fn(),
    onConfirm: vi.fn(),
    affectedAgentCount: null,
  };
  return render(<DeleteMcpServerModal {...defaults} {...props} />);
};

describe('DeleteMcpServerModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ─── Null guard ──────────────────────────────────────────────────────────────

  it('renders_nothing_when_server_is_null', () => {
    const { container } = render(
      <DeleteMcpServerModal
        server={null}
        isDeleting={false}
        error={null}
        onCancel={vi.fn()}
        onConfirm={vi.fn()}
        affectedAgentCount={null}
      />
    );
    expect(container.firstChild).toBeNull();
  });

  // ─── Scenario 5: Delete modal opens with server name ────────────────────────

  it('shows_modal_title_delete_mcp_server', () => {
    renderModal();
    expect(screen.getByRole('heading', { name: 'Delete MCP Server' })).toBeInTheDocument();
  });

  it('shows_server_name_in_confirmation_body', () => {
    renderModal({ server: makeServer({ name: 'My Figma Server' }) });
    expect(screen.getByText('My Figma Server')).toBeInTheDocument();
  });

  it('body_mentions_action_cannot_be_undone', () => {
    renderModal();
    expect(screen.getByText(/cannot be undone/i)).toBeInTheDocument();
  });

  // ─── Callback wiring ─────────────────────────────────────────────────────────

  it('clicking_cancel_button_invokes_onCancel', async () => {
    const onCancel = vi.fn();
    renderModal({ onCancel });
    await userEvent.click(screen.getByRole('button', { name: /cancel/i }));
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it('clicking_confirm_button_invokes_onConfirm', async () => {
    const onConfirm = vi.fn();
    renderModal({ onConfirm });
    await userEvent.click(screen.getByRole('button', { name: /delete server/i }));
    expect(onConfirm).toHaveBeenCalledOnce();
  });

  // ─── isDeleting state ────────────────────────────────────────────────────────

  it('confirm_button_shows_deleting_text_while_deleting', () => {
    renderModal({ isDeleting: true });
    expect(screen.getByRole('button', { name: /deleting/i })).toBeInTheDocument();
  });

  it('cancel_button_is_disabled_while_deleting', () => {
    renderModal({ isDeleting: true });
    expect(screen.getByRole('button', { name: /cancel/i })).toBeDisabled();
  });

  it('confirm_button_is_disabled_while_deleting', () => {
    renderModal({ isDeleting: true });
    const confirmButton = screen.getByRole('button', { name: /deleting/i });
    expect(confirmButton).toBeDisabled();
  });

  // ─── Error message ───────────────────────────────────────────────────────────

  it('does_not_render_error_when_error_is_null', () => {
    renderModal({ error: null });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('renders_error_message_with_role_alert', () => {
    renderModal({ error: 'Failed to delete. Please try again.' });
    const errorEl = screen.getByRole('alert');
    expect(errorEl).toBeInTheDocument();
    expect(errorEl).toHaveTextContent('Failed to delete. Please try again.');
  });

  // ─── Scenario 6: Keyboard accessibility — Escape key ────────────────────────

  it('pressing_escape_on_overlay_invokes_onCancel', () => {
    const onCancel = vi.fn();
    const { container } = renderModal({ onCancel });
    const overlay = container.firstChild as HTMLElement;
    fireEvent.keyDown(overlay, { key: 'Escape' });
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it('pressing_escape_does_not_invoke_onCancel_while_deleting', () => {
    const onCancel = vi.fn();
    const { container } = renderModal({ isDeleting: true, onCancel });
    const overlay = container.firstChild as HTMLElement;
    fireEvent.keyDown(overlay, { key: 'Escape' });
    expect(onCancel).not.toHaveBeenCalled();
  });

});
