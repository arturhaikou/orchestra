import React from 'react';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import DiscoveryLoadingScreen from '../mcp/DiscoveryLoadingScreen';
import DiscoveryResultsScreen from '../mcp/DiscoveryResultsScreen';
import ConnectionErrorScreen from '../mcp/ConnectionErrorScreen';
import { DiscoveredTool, McpDiscoveryError, ToolEnablementOverride } from '../../types';

const mockSafeTools: DiscoveredTool[] = [
  {
    id: 'tool-safe',
    name: 'get_file',
    description: 'Retrieve a Figma file',
    dangerLevel: 'Safe',
    mcpToolSchema: null,
  },
];

const mockDestructiveTool: DiscoveredTool = {
  id: 'tool-destructive',
  name: 'delete_component',
  description: 'Delete a Figma component permanently',
  dangerLevel: 'Destructive',
  mcpToolSchema: '{"parameters":[{"name":"componentId","type":"string","required":true}]}',
};

const mockMixedTools: DiscoveredTool[] = [mockSafeTools[0], mockDestructiveTool];

const mockFiveTools: DiscoveredTool[] = [
  { id: 'tool-1', name: 'read_file',       description: 'Read a file',       dangerLevel: 'Safe',     mcpToolSchema: null },
  { id: 'tool-2', name: 'list_directory',  description: 'List a directory',  dangerLevel: 'Safe',     mcpToolSchema: null },
  { id: 'tool-3', name: 'search_files',    description: 'Search files',      dangerLevel: 'Safe',     mcpToolSchema: null },
  { id: 'tool-4', name: 'write_file',      description: 'Write a file',      dangerLevel: 'Moderate', mcpToolSchema: null },
  { id: 'tool-5', name: 'execute_command', description: 'Execute a command', dangerLevel: 'Moderate', mcpToolSchema: null },
];

const mockMixedFiveTools: DiscoveredTool[] = [
  { id: 'tool-s1', name: 'read_file',   description: 'Read a file',   dangerLevel: 'Safe',        mcpToolSchema: null },
  { id: 'tool-s2', name: 'list_dir',    description: 'List dir',      dangerLevel: 'Safe',        mcpToolSchema: null },
  { id: 'tool-s3', name: 'search',      description: 'Search files',  dangerLevel: 'Safe',        mcpToolSchema: null },
  { id: 'tool-d1', name: 'delete_node', description: 'Delete a node', dangerLevel: 'Destructive', mcpToolSchema: null },
];

describe('DiscoveryLoadingScreen', () => {
  it('renders_loading_spinner', () => {
    render(<DiscoveryLoadingScreen providerName="Figma" endpointUrl="https://mcp.figma.com/mcp" />);

    expect(screen.getByTestId('discovery-loading')).toBeInTheDocument();
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('renders_provider_name_in_context', () => {
    render(<DiscoveryLoadingScreen providerName="Figma" endpointUrl="https://mcp.figma.com/mcp" />);

    expect(screen.getByText(/figma/i)).toBeInTheDocument();
  });

  it('renders_endpoint_url_as_disabled', () => {
    render(<DiscoveryLoadingScreen providerName="Figma" endpointUrl="https://mcp.figma.com/mcp" />);

    const urlDisplay = screen.getByDisplayValue('https://mcp.figma.com/mcp');
    expect(urlDisplay).toBeDisabled();
  });
});

describe('DiscoveryResultsScreen', () => {
  const mockOnConfirm = vi.fn();
  const mockOnCancel = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders_each_tool_name_and_description', () => {
    render(
      <DiscoveryResultsScreen
        tools={mockMixedTools}
        onConfirm={mockOnConfirm}
        onCancel={mockOnCancel}
      />
    );

    expect(screen.getByText('get_file')).toBeInTheDocument();
    expect(screen.getByText('Retrieve a Figma file')).toBeInTheDocument();
    expect(screen.getByText('delete_component')).toBeInTheDocument();
  });

  it('renders_destructive_danger_badge_for_destructive_tools', () => {
    render(
      <DiscoveryResultsScreen
        tools={mockMixedTools}
        onConfirm={mockOnConfirm}
        onCancel={mockOnCancel}
      />
    );

    expect(screen.getByText('Destructive')).toBeInTheDocument();
  });

  it('destructive_tool_toggle_defaults_to_off', () => {
    render(
      <DiscoveryResultsScreen
        tools={[mockDestructiveTool]}
        onConfirm={mockOnConfirm}
        onCancel={mockOnCancel}
      />
    );

    const toggle = screen.getByRole('checkbox', { name: /enable destructive tool delete_component/i });
    expect(toggle).not.toBeChecked();
  });

  it('safe_tool_toggle_defaults_to_on', () => {
    render(
      <DiscoveryResultsScreen
        tools={mockSafeTools}
        onConfirm={mockOnConfirm}
        onCancel={mockOnCancel}
      />
    );

    const toggle = screen.getByRole('checkbox', { name: /get_file/i });
    expect(toggle).toBeChecked();
  });

  it('destructive_toggle_has_accessible_aria_label', () => {
    render(
      <DiscoveryResultsScreen
        tools={[mockDestructiveTool]}
        onConfirm={mockOnConfirm}
        onCancel={mockOnCancel}
      />
    );

    expect(
      screen.getByRole('checkbox', { name: 'Enable destructive tool delete_component' })
    ).toBeInTheDocument();
  });

  it('user_can_enable_destructive_tool_via_toggle', async () => {
    render(
      <DiscoveryResultsScreen
        tools={[mockDestructiveTool]}
        onConfirm={mockOnConfirm}
        onCancel={mockOnCancel}
      />
    );

    const user = userEvent.setup();
    const toggle = screen.getByRole('checkbox', { name: /enable destructive tool delete_component/i });

    await user.click(toggle);

    expect(toggle).toBeChecked();
  });

  it('on_confirm_passes_override_array_with_correct_enabled_states', async () => {
    render(
      <DiscoveryResultsScreen
        tools={mockMixedTools}
        onConfirm={mockOnConfirm}
        onCancel={mockOnCancel}
      />
    );

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: /confirm & create integration/i }));

    expect(mockOnConfirm).toHaveBeenCalledWith(
      expect.arrayContaining<ToolEnablementOverride>([
        expect.objectContaining({ toolId: 'tool-safe', enabled: true }),
        expect.objectContaining({ toolId: 'tool-destructive', enabled: false }),
      ])
    );
  });

  it('shows_empty_state_when_no_tools', () => {
    render(
      <DiscoveryResultsScreen
        tools={[]}
        onConfirm={mockOnConfirm}
        onCancel={mockOnCancel}
      />
    );

    expect(screen.getByText(/no tools discovered/i)).toBeInTheDocument();
  });

  // --- BDD Scenario 1: Checking one tool does not select others (Regression Guard) ---
  it('checking_one_tool_does_not_check_remaining_tools', async () => {
    render(
      <DiscoveryResultsScreen
        tools={mockFiveTools}
        onConfirm={mockOnConfirm}
        onCancel={mockOnCancel}
      />
    );
    const user = userEvent.setup();

    for (const tool of mockFiveTools) {
      await user.click(screen.getByRole('checkbox', { name: tool.name }));
    }

    await user.click(screen.getByRole('checkbox', { name: 'read_file' }));

    expect(screen.getByRole('checkbox', { name: 'read_file' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'list_directory' })).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'search_files' })).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'write_file' })).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'execute_command' })).not.toBeChecked();
  });

  // --- BDD Scenario 2: Unchecking one tool does not deselect others (Regression Guard) ---
  it('unchecking_one_tool_does_not_deselect_remaining_tools', async () => {
    render(
      <DiscoveryResultsScreen
        tools={mockFiveTools}
        onConfirm={mockOnConfirm}
        onCancel={mockOnCancel}
      />
    );
    const user = userEvent.setup();

    await user.click(screen.getByRole('checkbox', { name: 'search_files' }));

    expect(screen.getByRole('checkbox', { name: 'read_file' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'list_directory' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'search_files' })).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'write_file' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'execute_command' })).toBeChecked();
  });

  // --- BDD Scenario 3: Destructive tool unaffected when safe tool is toggled (Regression Guard) ---
  it('checking_safe_tool_does_not_affect_destructive_tool_state', async () => {
    render(
      <DiscoveryResultsScreen
        tools={mockMixedFiveTools}
        onConfirm={mockOnConfirm}
        onCancel={mockOnCancel}
      />
    );
    const user = userEvent.setup();

    await user.click(screen.getByRole('checkbox', { name: 'search' }));
    await user.click(screen.getByRole('checkbox', { name: 'search' }));

    expect(
      screen.getByRole('checkbox', { name: 'Enable destructive tool delete_node' })
    ).not.toBeChecked();
  });

  // --- BDD Scenario 4: Only opted-in tools included in confirm payload (Regression Guard) ---
  it('confirm_payload_contains_only_tools_with_correct_enabled_values', async () => {
    render(
      <DiscoveryResultsScreen
        tools={mockFiveTools}
        onConfirm={mockOnConfirm}
        onCancel={mockOnCancel}
      />
    );
    const user = userEvent.setup();

    for (const tool of mockFiveTools) {
      await user.click(screen.getByRole('checkbox', { name: tool.name }));
    }
    await user.click(screen.getByRole('checkbox', { name: 'read_file' }));
    await user.click(screen.getByRole('checkbox', { name: 'search_files' }));

    await user.click(screen.getByRole('button', { name: /confirm & create integration/i }));

    const [overrides] = mockOnConfirm.mock.calls[0] as [ToolEnablementOverride[]];
    expect(overrides.find(o => o.toolId === 'tool-1')?.enabled).toBe(true);
    expect(overrides.find(o => o.toolId === 'tool-2')?.enabled).toBe(false);
    expect(overrides.find(o => o.toolId === 'tool-3')?.enabled).toBe(true);
    expect(overrides.find(o => o.toolId === 'tool-4')?.enabled).toBe(false);
    expect(overrides.find(o => o.toolId === 'tool-5')?.enabled).toBe(false);
  });

  // --- Count label: Red until task-002 is applied ---
  it('count_label_reflects_number_of_selected_tools', async () => {
    render(
      <DiscoveryResultsScreen
        tools={mockFiveTools}
        onConfirm={mockOnConfirm}
        onCancel={mockOnCancel}
      />
    );
    const user = userEvent.setup();

    expect(screen.getByText('5 of 5 tools selected')).toBeInTheDocument();

    await user.click(screen.getByRole('checkbox', { name: 'search_files' }));
    expect(screen.getByText('4 of 5 tools selected')).toBeInTheDocument();
  });

  // --- Updated empty state message: Red until task-002 is applied ---
  it('empty_state_shows_full_zero_capabilities_message', () => {
    render(
      <DiscoveryResultsScreen
        tools={[]}
        onConfirm={mockOnConfirm}
        onCancel={mockOnCancel}
      />
    );

    expect(
      screen.getByText('No tools discovered. The server responded but reported 0 capabilities.')
    ).toBeInTheDocument();
  });
});

describe('ConnectionErrorScreen', () => {
  const mockOnRetry = vi.fn();
  const mockOnBack = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders_connection_failed_message', () => {
    const error: McpDiscoveryError = {
      errorType: 'ConnectionFailed',
      message: 'Unable to reach the MCP server at https://bad.url',
    };

    render(<ConnectionErrorScreen error={error} onRetry={mockOnRetry} onBack={mockOnBack} />);

    expect(screen.getByText(/unable to reach/i)).toBeInTheDocument();
  });

  it('renders_auth_failed_message', () => {
    const error: McpDiscoveryError = {
      errorType: 'AuthFailed',
      message: 'API key rejected',
    };

    render(<ConnectionErrorScreen error={error} onRetry={mockOnRetry} onBack={mockOnBack} />);

    expect(screen.getByText(/api key was rejected/i)).toBeInTheDocument();
  });

  it('renders_timeout_message', () => {
    const error: McpDiscoveryError = {
      errorType: 'Timeout',
      message: 'Server timed out',
    };

    render(<ConnectionErrorScreen error={error} onRetry={mockOnRetry} onBack={mockOnBack} />);

    expect(screen.getByText(/did not respond within 30 seconds/i)).toBeInTheDocument();
  });

  it('renders_zero_tools_as_yellow_warning', () => {
    const error: McpDiscoveryError = {
      errorType: 'ZeroTools',
      message: 'Server returned no tools',
    };

    render(<ConnectionErrorScreen error={error} onRetry={mockOnRetry} onBack={mockOnBack} />);

    const banner = screen.getByTestId('connection-error');
    expect(banner).toHaveClass('bg-yellow');
    expect(screen.getByText(/did not advertise any tools/i)).toBeInTheDocument();
  });

  it('retry_button_calls_on_retry', async () => {
    const error: McpDiscoveryError = { errorType: 'ConnectionFailed', message: 'Unreachable' };

    render(<ConnectionErrorScreen error={error} onRetry={mockOnRetry} onBack={mockOnBack} />);

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: /retry connection/i }));

    expect(mockOnRetry).toHaveBeenCalledOnce();
  });

  it('back_button_calls_on_back', async () => {
    const error: McpDiscoveryError = { errorType: 'ConnectionFailed', message: 'Unreachable' };

    render(<ConnectionErrorScreen error={error} onRetry={mockOnRetry} onBack={mockOnBack} />);

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: /back/i }));

    expect(mockOnBack).toHaveBeenCalledOnce();
  });
});
