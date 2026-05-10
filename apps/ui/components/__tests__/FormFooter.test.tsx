import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import FormFooter from '../mcp/FormFooter';

describe('FormFooter', () => {
  const mockOnCancel = vi.fn();
  const mockOnSave = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders_cancel_button', () => {
    render(
      <FormFooter
        onCancel={mockOnCancel}
        onSave={mockOnSave}
        isSaveDisabled={false}
        isSaving={false}
      />
    );

    expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
  });

  it('renders_save_mcp_server_button', () => {
    render(
      <FormFooter
        onCancel={mockOnCancel}
        onSave={mockOnSave}
        isSaveDisabled={false}
        isSaving={false}
      />
    );

    expect(screen.getByRole('button', { name: /save mcp server/i })).toBeInTheDocument();
  });

  it('save_button_is_disabled_when_isSaveDisabled_is_true', () => {
    render(
      <FormFooter
        onCancel={mockOnCancel}
        onSave={mockOnSave}
        isSaveDisabled={true}
        isSaving={false}
      />
    );

    expect(screen.getByRole('button', { name: /save mcp server/i })).toBeDisabled();
  });

  it('save_button_has_connection_tooltip_when_disabled', () => {
    render(
      <FormFooter
        onCancel={mockOnCancel}
        onSave={mockOnSave}
        isSaveDisabled={true}
        isSaving={false}
      />
    );

    expect(screen.getByRole('button', { name: /save mcp server/i })).toHaveAttribute(
      'title',
      'Please verify the connection first'
    );
  });

  it('save_button_is_enabled_when_isSaveDisabled_is_false', () => {
    render(
      <FormFooter
        onCancel={mockOnCancel}
        onSave={mockOnSave}
        isSaveDisabled={false}
        isSaving={false}
      />
    );

    expect(screen.getByRole('button', { name: /save mcp server/i })).not.toBeDisabled();
  });

  it('calls_onCancel_when_cancel_is_clicked', async () => {
    const user = userEvent.setup();
    render(
      <FormFooter
        onCancel={mockOnCancel}
        onSave={mockOnSave}
        isSaveDisabled={false}
        isSaving={false}
      />
    );

    await user.click(screen.getByRole('button', { name: /cancel/i }));

    expect(mockOnCancel).toHaveBeenCalledOnce();
  });

  it('save_button_is_disabled_when_isSaving_is_true', () => {
    render(
      <FormFooter
        onCancel={mockOnCancel}
        onSave={mockOnSave}
        isSaveDisabled={false}
        isSaving={true}
      />
    );

    expect(screen.getByRole('button', { name: /save mcp server/i })).toBeDisabled();
  });
});
