import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import ServerIdentitySection from '../mcp/ServerIdentitySection';

describe('ServerIdentitySection', () => {
  const mockOnChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders_server_name_label', () => {
    render(
      <ServerIdentitySection serverName="" onChange={mockOnChange} />
    );

    expect(screen.getByLabelText(/server name/i)).toBeInTheDocument();
  });

  it('displays_current_server_name_value', () => {
    render(
      <ServerIdentitySection serverName="My Test Server" onChange={mockOnChange} />
    );

    expect(screen.getByLabelText<HTMLInputElement>(/server name/i).value).toBe('My Test Server');
  });

  it('calls_onChange_with_new_value_on_input', async () => {
    const user = userEvent.setup();
    render(
      <ServerIdentitySection serverName="" onChange={mockOnChange} />
    );

    await user.type(screen.getByLabelText(/server name/i), 'A');

    expect(mockOnChange).toHaveBeenCalledWith('A');
  });

  it('renders_name_error_message_when_provided', () => {
    render(
      <ServerIdentitySection
        serverName=""
        onChange={mockOnChange}
        nameError="Name is required"
      />
    );

    expect(screen.getByText('Name is required')).toBeInTheDocument();
  });

  it('does_not_render_error_when_nameError_is_undefined', () => {
    render(
      <ServerIdentitySection serverName="" onChange={mockOnChange} />
    );

    expect(screen.queryByText(/required/i)).not.toBeInTheDocument();
  });

  it('input_is_disabled_when_isDisabled_is_true', () => {
    render(
      <ServerIdentitySection serverName="" onChange={mockOnChange} isDisabled={true} />
    );

    expect(screen.getByLabelText(/server name/i)).toBeDisabled();
  });
});
