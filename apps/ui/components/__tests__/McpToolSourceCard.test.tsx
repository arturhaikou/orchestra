import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import McpToolSourceCard, { McpToolSourceCardProps } from '../agents/McpToolSourceCard';

describe('McpToolSourceCard — rendering', () => {
  const baseProps: McpToolSourceCardProps = {
    serverId: 'server-abc',
    serverName: 'My MCP Server',
    selectedToolNames: ['tool1', 'tool2', 'tool3'],
    totalToolCount: 10,
    connectionStatus: 'Connected',
    onEdit: vi.fn(),
    onRemove: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders_server_name', () => {
    render(<McpToolSourceCard {...baseProps} />);
    expect(screen.getByText('My MCP Server')).toBeInTheDocument();
  });

  it('renders_selection_count_badge_as_X_of_Y', () => {
    render(<McpToolSourceCard {...baseProps} />);
    const badgeText = screen.getByText(/3.*10/);
    expect(badgeText).toBeInTheDocument();
  });

  it('renders_MCP_visual_indicator', () => {
    const { container } = render(<McpToolSourceCard {...baseProps} />);
    const allMcpMatches = screen.getAllByText(/MCP/i);
    const mcpIndicator = allMcpMatches.length > 0 ? allMcpMatches[0] : container.querySelector('[aria-label*="MCP"]');
    expect(mcpIndicator).toBeInTheDocument();
  });

  it('renders_connection_status_badge', () => {
    render(<McpToolSourceCard {...baseProps} connectionStatus="Connected" />);
    expect(screen.getByText('Connected')).toBeInTheDocument();
  });

  it('renders_remove_button', () => {
    const { container } = render(<McpToolSourceCard {...baseProps} />);
    const removeButton = container.querySelector('[data-testid="remove-mcp-server-button"]') ||
      screen.queryByRole('button', { name: /remove/i });
    expect(removeButton).toBeInTheDocument();
  });
});

describe('McpToolSourceCard — interactions', () => {
  const baseProps: McpToolSourceCardProps = {
    serverId: 'server-abc',
    serverName: 'My MCP Server',
    selectedToolNames: ['tool1', 'tool2', 'tool3'],
    totalToolCount: 10,
    connectionStatus: 'Connected',
    onEdit: vi.fn(),
    onRemove: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls_onEdit_when_card_body_is_clicked', async () => {
    const onEdit = vi.fn();
    const onRemove = vi.fn();
    render(
      <McpToolSourceCard
        {...baseProps}
        onEdit={onEdit}
        onRemove={onRemove}
      />
    );

    const cardContainer = screen.getByTestId('mcp-tool-source-card');
    await userEvent.click(cardContainer);

    expect(onEdit).toHaveBeenCalledOnce();
    expect(onRemove).not.toHaveBeenCalled();
  });

  it('calls_onRemove_when_X_button_is_clicked', async () => {
    const onEdit = vi.fn();
    const onRemove = vi.fn();
    const { container } = render(
      <McpToolSourceCard
        {...baseProps}
        onEdit={onEdit}
        onRemove={onRemove}
      />
    );

    const removeButton = container.querySelector('[data-testid="remove-mcp-server-button"]') ||
      screen.getByRole('button', { name: /remove/i });
    
    await userEvent.click(removeButton as HTMLElement);

    expect(onRemove).toHaveBeenCalledOnce();
    expect(onEdit).not.toHaveBeenCalled();
  });

  it('onRemove_click_does_not_propagate_to_onEdit', async () => {
    const onEdit = vi.fn();
    const onRemove = vi.fn();
    const { container } = render(
      <McpToolSourceCard
        {...baseProps}
        onEdit={onEdit}
        onRemove={onRemove}
      />
    );

    const removeButton = container.querySelector('[data-testid="remove-mcp-server-button"]') ||
      screen.getByRole('button', { name: /remove/i });
    
    await userEvent.click(removeButton as HTMLElement);

    expect(onRemove).toHaveBeenCalledOnce();
    expect(onEdit).not.toHaveBeenCalled();
  });
});

describe('McpToolSourceCard — badge accuracy', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('badge_shows_correct_count_for_partial_selection', () => {
    const props: McpToolSourceCardProps = {
      serverId: 'server-xyz',
      serverName: 'Test Server',
      selectedToolNames: ['toolA', 'toolB'],
      totalToolCount: 5,
      connectionStatus: 'Connected',
      onEdit: vi.fn(),
      onRemove: vi.fn(),
    };

    render(<McpToolSourceCard {...props} />);
    expect(screen.getByText(/2.*5/)).toBeInTheDocument();
  });

  it('badge_shows_zero_selected_when_no_tools_selected', () => {
    const props: McpToolSourceCardProps = {
      serverId: 'server-def',
      serverName: 'Empty Server',
      selectedToolNames: [],
      totalToolCount: 7,
      connectionStatus: 'Connected',
      onEdit: vi.fn(),
      onRemove: vi.fn(),
    };

    render(<McpToolSourceCard {...props} />);
    expect(screen.getByText(/0.*7/)).toBeInTheDocument();
  });
});

describe('McpToolSourceCard — FR-006 visual enhancements', () => {
  const baseProps: McpToolSourceCardProps = {
    serverId: 'server-abc',
    serverName: 'My MCP Server',
    selectedToolNames: ['tool1', 'tool2', 'tool3'],
    totalToolCount: 10,
    connectionStatus: 'Connected',
    onEdit: vi.fn(),
    onRemove: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders_MCP_badge_text_as_strict_differentiator', () => {
    render(<McpToolSourceCard {...baseProps} />);
    expect(screen.getByText('MCP')).toBeInTheDocument();
  });

  it('card_root_has_hover_tailwind_variant_class', () => {
    const { container } = render(<McpToolSourceCard {...baseProps} />);
    const card = container.querySelector('[data-testid="mcp-tool-source-card"]');
    expect(card?.className).toContain('hover:');
  });

  it('card_root_has_active_tailwind_variant_class', () => {
    const { container } = render(<McpToolSourceCard {...baseProps} />);
    const card = container.querySelector('[data-testid="mcp-tool-source-card"]');
    expect(card?.className).toContain('active:');
  });

  it('card_root_has_motion_reduce_guard_on_transition', () => {
    const { container } = render(<McpToolSourceCard {...baseProps} />);
    const card = container.querySelector('[data-testid="mcp-tool-source-card"]');
    expect(card?.className).toContain('motion-reduce:');
  });
});
