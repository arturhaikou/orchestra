import React from 'react';
import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';
import McpIntegrationCard from '../mcp/McpIntegrationCard';
import SyncSummaryBanner from '../mcp/SyncSummaryBanner';
import McpToolRow from '../mcp/McpToolRow';
import * as integrationService from '../../services/integrationService';
import { McpIntegration, SyncToolsResult, ToolAction } from '../../types';

vi.mock('../../services/integrationService', () => ({
  syncIntegrationTools: vi.fn(),
  discoverMcpTools: vi.fn(),
  createMcpIntegration: vi.fn(),
  getIntegrations: vi.fn(),
  createIntegration: vi.fn(),
  updateIntegration: vi.fn(),
  deleteIntegration: vi.fn(),
}));

const mockFigmaIntegration: McpIntegration = {
  id: 'int-figma-1',
  workspaceId: 'ws-test',
  name: 'Figma Design',
  provider: 'FIGMA',
  types: [],
  icon: 'figma',
  isMcpBacked: true,
  mcpEndpointUrl: 'https://mcp.figma.com/mcp',
  mcpAuthType: 'ApiKey',
  toolCount: 6,
  connected: true,
  lastSync: '2 hours ago',
};

const mockDisconnectedIntegration: McpIntegration = {
  ...mockFigmaIntegration,
  id: 'int-disconnected',
  connected: false,
  toolCount: 0,
};

const mockSyncResult: SyncToolsResult = {
  added: 2,
  removed: 1,
  total: 7,
};

const mockSafeTool: ToolAction = {
  id: 'ta-safe',
  name: 'get_file',
  description: 'Retrieve a Figma file',
  dangerLevel: 'Safe',
  isEnabled: true,
  isMcpTool: true,
  mcpToolSchema: '{"parameters":[{"name":"fileKey","type":"string","required":true,"description":"The Figma file key"}]}',
  integrationId: 'int-figma-1',
};

const mockDestructiveTool: ToolAction = {
  ...mockSafeTool,
  id: 'ta-destructive',
  name: 'delete_component',
  dangerLevel: 'Destructive',
  isEnabled: false,
};

describe('McpIntegrationCard', () => {
  const mockOnSync = vi.fn();
  const mockOnEdit = vi.fn();
  const mockOnDelete = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders_integration_name', () => {
    render(
      <McpIntegrationCard
        integration={mockFigmaIntegration}
        onSync={mockOnSync}
        onEdit={mockOnEdit}
        onDelete={mockOnDelete}
      />
    );

    expect(screen.getByText('Figma Design')).toBeInTheDocument();
  });

  it('renders_provider_type_label', () => {
    render(
      <McpIntegrationCard
        integration={mockFigmaIntegration}
        onSync={mockOnSync}
        onEdit={mockOnEdit}
        onDelete={mockOnDelete}
      />
    );

    expect(screen.getAllByText(/figma/i).length).toBeGreaterThan(0);
  });

  it('renders_mcp_badge', () => {
    render(
      <McpIntegrationCard
        integration={mockFigmaIntegration}
        onSync={mockOnSync}
        onEdit={mockOnEdit}
        onDelete={mockOnDelete}
      />
    );

    expect(screen.getByText('MCP')).toBeInTheDocument();
  });

  it('renders_tool_count', () => {
    render(
      <McpIntegrationCard
        integration={mockFigmaIntegration}
        onSync={mockOnSync}
        onEdit={mockOnEdit}
        onDelete={mockOnDelete}
      />
    );

    expect(screen.getByText(/6 tools/i)).toBeInTheDocument();
  });

  it('renders_connected_status_indicator', () => {
    render(
      <McpIntegrationCard
        integration={mockFigmaIntegration}
        onSync={mockOnSync}
        onEdit={mockOnEdit}
        onDelete={mockOnDelete}
      />
    );

    expect(screen.getByTestId('connected-status')).toHaveClass('bg-green');
  });

  it('renders_sync_tools_button', () => {
    render(
      <McpIntegrationCard
        integration={mockFigmaIntegration}
        onSync={mockOnSync}
        onEdit={mockOnEdit}
        onDelete={mockOnDelete}
      />
    );

    expect(screen.getByRole('button', { name: /sync tools/i })).toBeInTheDocument();
  });

  it('clicking_sync_tools_calls_on_sync', async () => {
    vi.mocked(integrationService.syncIntegrationTools).mockResolvedValue(mockSyncResult);

    render(
      <McpIntegrationCard
        integration={mockFigmaIntegration}
        onSync={mockOnSync}
        onEdit={mockOnEdit}
        onDelete={mockOnDelete}
      />
    );

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: /sync tools/i }));

    expect(mockOnSync).toHaveBeenCalledOnce();
  });

  it('renders_disconnected_status_when_not_connected', () => {
    render(
      <McpIntegrationCard
        integration={mockDisconnectedIntegration}
        onSync={mockOnSync}
        onEdit={mockOnEdit}
        onDelete={mockOnDelete}
      />
    );

    expect(screen.getByTestId('connected-status')).not.toHaveClass('bg-green');
  });
});

describe('SyncSummaryBanner', () => {
  const mockOnDismiss = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders_added_removed_total_counts', () => {
    render(<SyncSummaryBanner result={mockSyncResult} onDismiss={mockOnDismiss} />);

    expect(screen.getByText(/2 added/i)).toBeInTheDocument();
    expect(screen.getByText(/1 removed/i)).toBeInTheDocument();
    expect(screen.getByText(/7 total/i)).toBeInTheDocument();
  });

  it('renders_dismiss_button', () => {
    render(<SyncSummaryBanner result={mockSyncResult} onDismiss={mockOnDismiss} />);

    expect(screen.getByRole('button', { name: /dismiss/i })).toBeInTheDocument();
  });

  it('dismiss_button_calls_on_dismiss', async () => {
    render(<SyncSummaryBanner result={mockSyncResult} onDismiss={mockOnDismiss} />);

    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    await user.click(screen.getByRole('button', { name: /dismiss/i }));

    expect(mockOnDismiss).toHaveBeenCalledOnce();
  });

  it('auto_dismisses_after_8_seconds', () => {
    render(<SyncSummaryBanner result={mockSyncResult} onDismiss={mockOnDismiss} />);

    act(() => {
      vi.advanceTimersByTime(8000);
    });

    expect(mockOnDismiss).toHaveBeenCalledOnce();
  });

  it('does_not_dismiss_before_8_seconds', () => {
    render(<SyncSummaryBanner result={mockSyncResult} onDismiss={mockOnDismiss} />);

    act(() => {
      vi.advanceTimersByTime(7999);
    });

    expect(mockOnDismiss).not.toHaveBeenCalled();
  });
});

describe('McpToolRow', () => {
  const mockOnToggle = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders_tool_name', () => {
    render(<McpToolRow tool={mockSafeTool} onToggle={mockOnToggle} />);

    expect(screen.getByText('get_file')).toBeInTheDocument();
  });

  it('renders_mcp_badge', () => {
    render(<McpToolRow tool={mockSafeTool} onToggle={mockOnToggle} />);

    expect(screen.getByText('MCP')).toBeInTheDocument();
  });

  it('renders_safe_danger_badge_in_green', () => {
    render(<McpToolRow tool={mockSafeTool} onToggle={mockOnToggle} />);

    const badge = screen.getByTestId('danger-badge');
    expect(badge).toHaveClass('bg-green');
    expect(badge).toHaveTextContent('Safe');
  });

  it('renders_destructive_danger_badge_in_red', () => {
    render(<McpToolRow tool={mockDestructiveTool} onToggle={mockOnToggle} />);

    const badge = screen.getByTestId('danger-badge');
    expect(badge).toHaveClass('bg-red');
    expect(badge).toHaveTextContent('Destructive');
  });

  it('renders_expand_chevron_button', () => {
    render(<McpToolRow tool={mockSafeTool} onToggle={mockOnToggle} />);

    expect(screen.getByRole('button', { name: /expand schema/i })).toBeInTheDocument();
  });

  it('clicking_expand_reveals_schema_parameters', async () => {
    render(<McpToolRow tool={mockSafeTool} onToggle={mockOnToggle} />);

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: /expand schema/i }));

    expect(screen.getByText('fileKey')).toBeInTheDocument();
    expect(screen.getByText('string')).toBeInTheDocument();
  });

  it('assignment_toggle_calls_on_toggle_with_correct_values', async () => {
    render(<McpToolRow tool={mockSafeTool} onToggle={mockOnToggle} />);

    const user = userEvent.setup();
    await user.click(screen.getByRole('checkbox', { name: /assign get_file/i }));

    expect(mockOnToggle).toHaveBeenCalledWith('ta-safe', false);
  });

  it('destructive_disabled_tool_toggle_is_aria_disabled', () => {
    render(<McpToolRow tool={mockDestructiveTool} onToggle={mockOnToggle} />);

    const toggle = screen.getByRole('checkbox', { name: /assign delete_component/i });
    expect(toggle).toHaveAttribute('aria-disabled', 'true');
  });
});
