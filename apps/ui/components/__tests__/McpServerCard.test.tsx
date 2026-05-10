import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import McpServerCard from '../mcp/McpServerCard';
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

describe('McpServerCard', () => {
  const onEdit = vi.fn();
  const onDelete = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ─── Scenario 1: All required information displayed ──────────────────────────

  it('renders_server_name', () => {
    render(<McpServerCard server={makeServer()} onEdit={onEdit} onDelete={onDelete} />);
    expect(screen.getByText('My Figma Server')).toBeInTheDocument();
  });

  it('renders_connection_status_badge_via_status_badge_component', () => {
    render(
      <McpServerCard
        server={makeServer({ connectionStatus: 'Connected' })}
        onEdit={onEdit}
        onDelete={onDelete}
      />
    );
    expect(screen.getByLabelText('Connection status: Connected')).toBeInTheDocument();
  });

  it('renders_http_transport_badge', () => {
    render(<McpServerCard server={makeServer({ transportType: 'HTTP' })} onEdit={onEdit} onDelete={onDelete} />);
    expect(screen.getByLabelText('Transport: HTTP')).toBeInTheDocument();
  });

  it('renders_stdio_transport_badge', () => {
    render(
      <McpServerCard
        server={makeServer({ transportType: 'STDIO', command: 'npx mcp-server' })}
        onEdit={onEdit}
        onDelete={onDelete}
      />
    );
    expect(screen.getByLabelText(/transport/i)).toBeInTheDocument();
  });

  it('renders_edit_icon_button_with_aria_label', () => {
    render(<McpServerCard server={makeServer()} onEdit={onEdit} onDelete={onDelete} />);
    expect(screen.getByRole('button', { name: 'Edit server' })).toBeInTheDocument();
  });

  it('renders_delete_icon_button_with_aria_label', () => {
    render(<McpServerCard server={makeServer()} onEdit={onEdit} onDelete={onDelete} />);
    expect(screen.getByRole('button', { name: 'Delete server' })).toBeInTheDocument();
  });

  // ─── Scenario 2: Long name truncated with tooltip ────────────────────────────

  it('sets_title_attribute_to_full_server_name_for_tooltip', () => {
    const longName = 'A'.repeat(50);
    render(<McpServerCard server={makeServer({ name: longName })} onEdit={onEdit} onDelete={onDelete} />);
    const nameElement = screen.getByTitle(longName);
    expect(nameElement).toBeInTheDocument();
  });

  // ─── Scenario 3: Status badge shows each state ───────────────────────────────

  it('status_badge_shows_connection_failed_label', () => {
    render(
      <McpServerCard
        server={makeServer({ connectionStatus: 'ConnectionFailed' })}
        onEdit={onEdit}
        onDelete={onDelete}
      />
    );
    expect(screen.getByLabelText('Connection status: Connection Failed')).toBeInTheDocument();
  });

  it('status_badge_shows_unverified_label', () => {
    render(
      <McpServerCard
        server={makeServer({ connectionStatus: 'Unverified' })}
        onEdit={onEdit}
        onDelete={onDelete}
      />
    );
    expect(screen.getByLabelText('Connection status: Unverified')).toBeInTheDocument();
  });

  // ─── Scenario 4: Edit button callback ────────────────────────────────────────

  it('clicking_edit_button_invokes_onEdit', async () => {
    render(<McpServerCard server={makeServer()} onEdit={onEdit} onDelete={onDelete} />);
    await userEvent.click(screen.getByRole('button', { name: 'Edit server' }));
    expect(onEdit).toHaveBeenCalledOnce();
  });

  // ─── Scenario 5: Delete button callback ──────────────────────────────────────

  it('clicking_delete_button_invokes_onDelete', async () => {
    render(<McpServerCard server={makeServer()} onEdit={onEdit} onDelete={onDelete} />);
    await userEvent.click(screen.getByRole('button', { name: 'Delete server' }));
    expect(onDelete).toHaveBeenCalledOnce();
  });

  // ─── Scenario 6: Keyboard accessibility ──────────────────────────────────────

  it('pressing_enter_on_focused_edit_button_invokes_onEdit', async () => {
    render(<McpServerCard server={makeServer()} onEdit={onEdit} onDelete={onDelete} />);
    const editButton = screen.getByRole('button', { name: 'Edit server' });
    editButton.focus();
    await userEvent.keyboard('{Enter}');
    expect(onEdit).toHaveBeenCalledOnce();
  });

  it('pressing_enter_on_focused_delete_button_invokes_onDelete', async () => {
    render(<McpServerCard server={makeServer()} onEdit={onEdit} onDelete={onDelete} />);
    const deleteButton = screen.getByRole('button', { name: 'Delete server' });
    deleteButton.focus();
    await userEvent.keyboard('{Enter}');
    expect(onDelete).toHaveBeenCalledOnce();
  });

  it('edit_button_has_title_tooltip', () => {
    render(<McpServerCard server={makeServer()} onEdit={onEdit} onDelete={onDelete} />);
    expect(screen.getByTitle('Edit server')).toBeInTheDocument();
  });

  it('delete_button_has_title_tooltip', () => {
    render(<McpServerCard server={makeServer()} onEdit={onEdit} onDelete={onDelete} />);
    expect(screen.getByTitle('Delete server')).toBeInTheDocument();
  });
});
