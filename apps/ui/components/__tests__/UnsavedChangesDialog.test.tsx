import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import UnsavedChangesDialog from '../mcp/UnsavedChangesDialog';

describe('UnsavedChangesDialog', () => {
  const mockOnStay = vi.fn();
  const mockOnLeave = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders_dialog_when_isOpen_is_true', () => {
    render(
      <UnsavedChangesDialog isOpen={true} onStay={mockOnStay} onLeave={mockOnLeave} />
    );

    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('does_not_render_dialog_when_isOpen_is_false', () => {
    render(
      <UnsavedChangesDialog isOpen={false} onStay={mockOnStay} onLeave={mockOnLeave} />
    );

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders_stay_on_page_button', () => {
    render(
      <UnsavedChangesDialog isOpen={true} onStay={mockOnStay} onLeave={mockOnLeave} />
    );

    expect(screen.getByRole('button', { name: /stay on page/i })).toBeInTheDocument();
  });

  it('renders_leave_without_saving_button', () => {
    render(
      <UnsavedChangesDialog isOpen={true} onStay={mockOnStay} onLeave={mockOnLeave} />
    );

    expect(screen.getByRole('button', { name: /leave without saving/i })).toBeInTheDocument();
  });

  it('calls_onStay_when_stay_on_page_is_clicked', async () => {
    const user = userEvent.setup();
    render(
      <UnsavedChangesDialog isOpen={true} onStay={mockOnStay} onLeave={mockOnLeave} />
    );

    await user.click(screen.getByRole('button', { name: /stay on page/i }));

    expect(mockOnStay).toHaveBeenCalledOnce();
  });

  it('calls_onLeave_when_leave_without_saving_is_clicked', async () => {
    const user = userEvent.setup();
    render(
      <UnsavedChangesDialog isOpen={true} onStay={mockOnStay} onLeave={mockOnLeave} />
    );

    await user.click(screen.getByRole('button', { name: /leave without saving/i }));

    expect(mockOnLeave).toHaveBeenCalledOnce();
  });

  it('dialog_has_aria_modal_true', () => {
    render(
      <UnsavedChangesDialog isOpen={true} onStay={mockOnStay} onLeave={mockOnLeave} />
    );

    expect(screen.getByRole('dialog')).toHaveAttribute('aria-modal', 'true');
  });
});
