import React from 'react';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import StdioConnectionFields from '../mcp/StdioConnectionFields';
import {
  McpServerStdioFields,
  StdioFieldErrors,
  StdioFieldTouched,
  EnvVarEditStateMap,
  EnvVarValueEditState,
} from '../../types';

// ── Service mock ──────────────────────────────────────────────────────────────

vi.mock('../../services/mcpServerService', () => ({
  checkMcpServerNameUnique: vi.fn().mockResolvedValue({ isUnique: true }),
}));

// ── Default props factory ─────────────────────────────────────────────────────

const defaultFields = (): McpServerStdioFields & { serverName: string } => ({
  serverName: 'My Server',
  command: 'npx',
  args: [],
  envVars: [],
});

const defaultErrors = (): StdioFieldErrors => ({});

const defaultTouched = (): StdioFieldTouched => ({
  serverName: false,
  command: false,
  argTouched: {},
  envKeyTouched: {},
});

interface RenderProps {
  fields?: McpServerStdioFields & { serverName: string };
  errors?: StdioFieldErrors;
  touched?: StdioFieldTouched;
  isEditMode?: boolean;
  envVarEditStateMap?: EnvVarEditStateMap;
  isCheckingName?: boolean;
  isDisabled?: boolean;
  onChange?: ReturnType<typeof vi.fn>;
  onBlur?: ReturnType<typeof vi.fn>;
  onEnvVarEditStateChange?: ReturnType<typeof vi.fn>;
}

const renderStdioFields = (overrides: RenderProps = {}) => {
  const props = {
    fields: overrides.fields ?? defaultFields(),
    errors: overrides.errors ?? defaultErrors(),
    touched: overrides.touched ?? defaultTouched(),
    isEditMode: overrides.isEditMode ?? false,
    envVarEditStateMap: overrides.envVarEditStateMap,
    isCheckingName: overrides.isCheckingName ?? false,
    isDisabled: overrides.isDisabled ?? false,
    onChange: (overrides.onChange ?? vi.fn()) as (patch: Partial<McpServerStdioFields & { serverName: string }>) => void,
    onBlur: (overrides.onBlur ?? vi.fn()) as (field: keyof StdioFieldTouched | 'arg' | 'envKey', index?: number) => void,
    onEnvVarEditStateChange: (overrides.onEnvVarEditStateChange ?? vi.fn()) as (rowIndex: number, state: EnvVarValueEditState) => void,
  };
  return render(<StdioConnectionFields {...props} />);
};

beforeEach(() => vi.clearAllMocks());

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 1: All required fields valid — Connect enabled
// ─────────────────────────────────────────────────────────────────────────────

describe('Scenario 1: All Stdio required fields valid — no inline errors shown', () => {
  it('renders_server_name_input', () => {
    renderStdioFields();
    expect(screen.getByLabelText(/server name/i)).toBeInTheDocument();
  });

  it('renders_command_input', () => {
    renderStdioFields();
    expect(screen.getByLabelText(/command/i)).toBeInTheDocument();
  });

  it('shows_no_errors_when_fields_valid', () => {
    renderStdioFields({
      touched: { serverName: true, command: true, argTouched: {}, envKeyTouched: {} },
    });
    expect(screen.queryByText('Command is required.')).not.toBeInTheDocument();
    expect(screen.queryByText('Server name is required.')).not.toBeInTheDocument();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 2: Empty command field is rejected
// ─────────────────────────────────────────────────────────────────────────────

describe('Scenario 2: Empty command field shows error after blur', () => {
  it('shows_command_required_error_when_touched_and_empty', () => {
    renderStdioFields({
      fields: { ...defaultFields(), command: '' },
      errors: { command: 'Command is required.' },
      touched: { ...defaultTouched(), command: true },
    });
    expect(screen.getByText('Command is required.')).toBeInTheDocument();
  });

  it('calls_onBlur_with_command_when_command_field_blurred', async () => {
    const onBlur = vi.fn();
    renderStdioFields({ onBlur });
    await userEvent.tab();
    const commandInput = screen.getByLabelText(/command/i);
    commandInput.focus();
    await userEvent.tab();
    expect(onBlur).toHaveBeenCalledWith('command');
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 3: User adds and removes arguments
// ─────────────────────────────────────────────────────────────────────────────

describe('Scenario 3: User adds and removes arguments', () => {
  it('shows_empty_placeholder_when_no_args', () => {
    renderStdioFields({ fields: { ...defaultFields(), args: [] } });
    expect(screen.getByText(/no arguments yet/i)).toBeInTheDocument();
  });

  it('calls_onChange_with_new_arg_when_add_argument_clicked', async () => {
    const onChange = vi.fn();
    renderStdioFields({ onChange });
    await userEvent.click(screen.getByText(/add argument/i));
    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ args: [''] })
    );
  });

  it('shows_arg_row_when_args_contain_one_entry', () => {
    renderStdioFields({ fields: { ...defaultFields(), args: ['-y'] } });
    expect(screen.getByDisplayValue('-y')).toBeInTheDocument();
  });

  it('calls_onChange_with_arg_removed_when_remove_button_clicked', async () => {
    const onChange = vi.fn();
    renderStdioFields({
      fields: { ...defaultFields(), args: ['-y'] },
      onChange,
    });
    const removeButton = screen.getByRole('button', { name: /remove argument 0/i });
    await userEvent.click(removeButton);
    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ args: [] })
    );
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 4: Arguments are reorderable
// ─────────────────────────────────────────────────────────────────────────────

describe('Scenario 4: Arguments are reorderable via up/down controls', () => {
  it('calls_onChange_with_swapped_args_when_move_down_clicked_on_first_row', async () => {
    const onChange = vi.fn();
    renderStdioFields({
      fields: { ...defaultFields(), args: ['arg1', 'arg2'] },
      onChange,
    });
    const moveDownButton = screen.getByRole('button', {
      name: /move argument 0 down/i,
    });
    await userEvent.click(moveDownButton);
    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ args: ['arg2', 'arg1'] })
    );
  });

  it('calls_onChange_with_swapped_args_when_move_up_clicked_on_second_row', async () => {
    const onChange = vi.fn();
    renderStdioFields({
      fields: { ...defaultFields(), args: ['arg1', 'arg2'] },
      onChange,
    });
    const moveUpButton = screen.getByRole('button', {
      name: /move argument 1 up/i,
    });
    await userEvent.click(moveUpButton);
    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ args: ['arg2', 'arg1'] })
    );
  });

  it('disables_move_up_on_first_row', () => {
    renderStdioFields({
      fields: { ...defaultFields(), args: ['arg1', 'arg2'] },
    });
    const moveUpFirst = screen.getByRole('button', { name: /move argument 0 up/i });
    expect(moveUpFirst).toBeDisabled();
  });

  it('disables_move_down_on_last_row', () => {
    renderStdioFields({
      fields: { ...defaultFields(), args: ['arg1', 'arg2'] },
    });
    const moveDownLast = screen.getByRole('button', { name: /move argument 1 down/i });
    expect(moveDownLast).toBeDisabled();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 5: Shell operators in command rejected
// ─────────────────────────────────────────────────────────────────────────────

describe('Scenario 5: Shell operators in command are rejected', () => {
  it('shows_shell_operator_error_when_touched_and_error_present', () => {
    renderStdioFields({
      fields: { ...defaultFields(), command: 'npx && rm -rf /' },
      errors: {
        command:
          'Shell operators are not allowed. Enter a single executable name.',
      },
      touched: { ...defaultTouched(), command: true },
    });
    expect(
      screen.getByText(
        'Shell operators are not allowed. Enter a single executable name.'
      )
    ).toBeInTheDocument();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 6: Env var key with invalid characters rejected
// ─────────────────────────────────────────────────────────────────────────────

describe('Scenario 6: Env var key with invalid characters is rejected', () => {
  it('shows_invalid_key_error_when_touched_and_error_present', () => {
    renderStdioFields({
      fields: {
        ...defaultFields(),
        envVars: [{ key: 'MY VAR', value: '' }],
      },
      errors: {
        envKeyErrors: {
          0: 'Key must contain only letters, numbers, and underscores.',
        },
      },
      touched: {
        ...defaultTouched(),
        envKeyTouched: { 0: true },
      },
    });
    expect(
      screen.getByText('Key must contain only letters, numbers, and underscores.')
    ).toBeInTheDocument();
  });

  it('calls_onBlur_with_envKey_and_index_when_key_blurred', async () => {
    const onBlur = vi.fn();
    renderStdioFields({
      fields: { ...defaultFields(), envVars: [{ key: '', value: '' }] },
      onBlur,
    });
    const keyInput = screen.getByRole('textbox', { name: /environment variable key 0/i });
    keyInput.focus();
    await userEvent.tab();
    expect(onBlur).toHaveBeenCalledWith('envKey', 0);
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 7: Existing env var values are masked on edit form
// ─────────────────────────────────────────────────────────────────────────────

describe('Scenario 7: Existing env var values are masked in edit mode', () => {
  it('renders_value_input_as_password_type_when_state_is_masked', () => {
    const envVarEditStateMap: EnvVarEditStateMap = { 0: 'masked' };
    renderStdioFields({
      isEditMode: true,
      fields: {
        ...defaultFields(),
        envVars: [{ key: 'GITHUB_TOKEN', value: '' }],
      },
      envVarEditStateMap,
    });
    const valueInput = screen.getByRole('textbox', {
      name: /environment variable value 0/i,
      hidden: true,
    }) as HTMLInputElement;
    expect(valueInput.type).toBe('password');
  });

  it('shows_key_in_plain_text_when_state_is_masked', () => {
    const envVarEditStateMap: EnvVarEditStateMap = { 0: 'masked' };
    renderStdioFields({
      isEditMode: true,
      fields: {
        ...defaultFields(),
        envVars: [{ key: 'GITHUB_TOKEN', value: '' }],
      },
      envVarEditStateMap,
    });
    expect(screen.getByDisplayValue('GITHUB_TOKEN')).toBeInTheDocument();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 8: Editing a connection-relevant field resets verified state
// ─────────────────────────────────────────────────────────────────────────────

describe('Scenario 8: Changing command calls onChange to reset verified state', () => {
  it('calls_onChange_when_command_input_changes', async () => {
    const onChange = vi.fn();
    renderStdioFields({
      fields: { ...defaultFields(), command: 'npx' },
      onChange,
    });
    const commandInput = screen.getByLabelText(/command/i);
    await userEvent.clear(commandInput);
    await userEvent.type(commandInput, 'docker');
    expect(onChange).toHaveBeenCalled();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 9: Leaving a masked env var unchanged does NOT call onEnvVarEditStateChange
// ─────────────────────────────────────────────────────────────────────────────

describe('Scenario 9: Masked env var not interacted with does not change edit state', () => {
  it('does_not_call_onEnvVarEditStateChange_when_masked_value_field_not_touched', () => {
    const onEnvVarEditStateChange = vi.fn();
    const envVarEditStateMap: EnvVarEditStateMap = { 0: 'masked' };
    renderStdioFields({
      isEditMode: true,
      fields: {
        ...defaultFields(),
        envVars: [{ key: 'GITHUB_TOKEN', value: '' }],
      },
      envVarEditStateMap,
      onEnvVarEditStateChange,
    });
    expect(onEnvVarEditStateChange).not.toHaveBeenCalled();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 10: Clearing and retyping a masked env var value calls onEnvVarEditStateChange
// ─────────────────────────────────────────────────────────────────────────────

describe('Scenario 10: Clearing and retyping masked env var value marks row as touched', () => {
  it('calls_onEnvVarEditStateChange_with_touched_when_masked_value_is_cleared_and_retyped', async () => {
    const onEnvVarEditStateChange = vi.fn();
    const envVarEditStateMap: EnvVarEditStateMap = { 0: 'masked' };
    renderStdioFields({
      isEditMode: true,
      fields: {
        ...defaultFields(),
        envVars: [{ key: 'GITHUB_TOKEN', value: '' }],
      },
      envVarEditStateMap,
      onEnvVarEditStateChange,
    });
    const valueInput = screen.getByRole('textbox', {
      name: /environment variable value 0/i,
      hidden: true,
    });
    await userEvent.clear(valueInput);
    await userEvent.type(valueInput, 'new-secret');
    expect(onEnvVarEditStateChange).toHaveBeenCalledWith(0, 'touched');
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 11: Server name whitespace only is rejected
// ─────────────────────────────────────────────────────────────────────────────

describe('Scenario 11: Server name containing only whitespace is rejected', () => {
  it('shows_server_name_required_error_when_touched_and_error_present', () => {
    renderStdioFields({
      fields: { ...defaultFields(), serverName: '   ' },
      errors: { serverName: 'Server name is required.' },
      touched: { ...defaultTouched(), serverName: true },
    });
    expect(screen.getByText('Server name is required.')).toBeInTheDocument();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 12: Command > 500 characters rejected
// ─────────────────────────────────────────────────────────────────────────────

describe('Scenario 12: Command field exceeding 500 characters is rejected', () => {
  it('shows_command_too_long_error_when_touched_and_error_present', () => {
    renderStdioFields({
      fields: { ...defaultFields(), command: 'a'.repeat(501) },
      errors: { command: 'Command must not exceed 500 characters.' },
      touched: { ...defaultTouched(), command: true },
    });
    expect(
      screen.getByText('Command must not exceed 500 characters.')
    ).toBeInTheDocument();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 13: A single argument exceeding 1,000 characters is rejected
// ─────────────────────────────────────────────────────────────────────────────

describe('Scenario 13: Argument exceeding 1,000 characters is rejected', () => {
  it('shows_arg_too_long_error_when_touched_and_error_present', () => {
    renderStdioFields({
      fields: { ...defaultFields(), args: ['a'.repeat(1001)] },
      errors: {
        argErrors: {
          0: 'Argument exceeds the maximum length of 1,000 characters.',
        },
      },
      touched: {
        ...defaultTouched(),
        argTouched: { 0: true },
      },
    });
    expect(
      screen.getByText('Argument exceeds the maximum length of 1,000 characters.')
    ).toBeInTheDocument();
  });

  it('calls_onBlur_with_arg_and_index_when_arg_input_blurred', async () => {
    const onBlur = vi.fn();
    renderStdioFields({
      fields: { ...defaultFields(), args: ['-y'] },
      onBlur,
    });
    const argInput = screen.getByRole('textbox', { name: /argument 0/i });
    argInput.focus();
    await userEvent.tab();
    expect(onBlur).toHaveBeenCalledWith('arg', 0);
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Scenario 14: Adding more than 50 arguments is prevented
// ─────────────────────────────────────────────────────────────────────────────

describe('Scenario 14: Adding more than 50 arguments is prevented', () => {
  it('disables_add_argument_button_when_50_args_present', () => {
    renderStdioFields({
      fields: { ...defaultFields(), args: Array(50).fill('arg') },
    });
    expect(screen.getByRole('button', { name: /add argument/i })).toBeDisabled();
  });

  it('does_not_add_51st_arg_when_add_argument_clicked_at_limit', async () => {
    const onChange = vi.fn();
    renderStdioFields({
      fields: { ...defaultFields(), args: Array(50).fill('arg') },
      onChange,
    });
    await userEvent.click(screen.getByRole('button', { name: /add argument/i }));
    expect(onChange).not.toHaveBeenCalled();
  });
});
