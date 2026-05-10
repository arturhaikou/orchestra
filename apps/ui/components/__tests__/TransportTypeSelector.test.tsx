import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import TransportTypeSelector from '../mcp/TransportTypeSelector';

describe('TransportTypeSelector', () => {
  const mockOnChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders_http_and_stdio_options', () => {
    render(
      <TransportTypeSelector value="http" onChange={mockOnChange} />
    );

    expect(screen.getByRole('radio', { name: /http/i })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /stdio/i })).toBeInTheDocument();
  });

  it('marks_http_as_checked_when_value_is_http', () => {
    render(
      <TransportTypeSelector value="http" onChange={mockOnChange} />
    );

    expect(screen.getByRole('radio', { name: /http/i })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByRole('radio', { name: /stdio/i })).toHaveAttribute('aria-checked', 'false');
  });

  it('marks_stdio_as_checked_when_value_is_stdio', () => {
    render(
      <TransportTypeSelector value="stdio" onChange={mockOnChange} />
    );

    expect(screen.getByRole('radio', { name: /stdio/i })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByRole('radio', { name: /http/i })).toHaveAttribute('aria-checked', 'false');
  });

  it('calls_onChange_with_stdio_when_stdio_is_clicked', async () => {
    const user = userEvent.setup();
    render(
      <TransportTypeSelector value="http" onChange={mockOnChange} />
    );

    await user.click(screen.getByRole('radio', { name: /stdio/i }));

    expect(mockOnChange).toHaveBeenCalledWith('stdio');
  });

  it('calls_onChange_with_http_when_http_is_clicked', async () => {
    const user = userEvent.setup();
    render(
      <TransportTypeSelector value="stdio" onChange={mockOnChange} />
    );

    await user.click(screen.getByRole('radio', { name: /http/i }));

    expect(mockOnChange).toHaveBeenCalledWith('http');
  });

  it('renders_disabled_buttons_when_isDisabled_is_true', () => {
    render(
      <TransportTypeSelector value="http" onChange={mockOnChange} isDisabled={true} />
    );

    expect(screen.getByRole('radio', { name: /http/i })).toBeDisabled();
    expect(screen.getByRole('radio', { name: /stdio/i })).toBeDisabled();
  });

  it('does_not_call_onChange_when_disabled_button_is_clicked', async () => {
    const user = userEvent.setup();
    render(
      <TransportTypeSelector value="http" onChange={mockOnChange} isDisabled={true} />
    );

    await user.click(screen.getByRole('radio', { name: /stdio/i }));

    expect(mockOnChange).not.toHaveBeenCalled();
  });
});
