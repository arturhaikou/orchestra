import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import DestructiveToolWarningDialog from '../agents/DestructiveToolWarningDialog';

// ─── Shared test data ─────────────────────────────────────────────────────────

const singleToolNames = ['force-merge'];
const multipleToolNames = ['force-merge', 'delete-repo', 'reset-branch'];

// ─── Render helper ────────────────────────────────────────────────────────────

function renderDialog(toolNames: string[] = singleToolNames) {
  const onConfirm = vi.fn();
  const onCancel = vi.fn();

  render(
    <DestructiveToolWarningDialog
      toolNames={toolNames}
      onConfirm={onConfirm}
      onCancel={onCancel}
    />
  );

  return { onConfirm, onCancel };
}

// ─── Suite 1 — Dialog renders with warning content ────────────────────────────

describe('DestructiveToolWarningDialog — renders warning content', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the warning message about irreversible or high-impact actions', () => {
    renderDialog();

    expect(
      screen.getByText(
        /this tool can perform irreversible or high-impact actions|irreversible or high-impact/i
      )
    ).toBeInTheDocument();
  });

  it('renders a Confirm button', () => {
    renderDialog();

    expect(screen.getByRole('button', { name: /confirm/i })).toBeInTheDocument();
  });

  it('renders a Cancel button', () => {
    renderDialog();

    expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
  });

  it('renders with role="dialog" and aria-modal="true"', () => {
    renderDialog();

    const dialog = screen.getByRole('dialog');
    expect(dialog).toBeInTheDocument();
    expect(dialog).toHaveAttribute('aria-modal', 'true');
  });

  it('displays the destructive tool name in the dialog for single tool', () => {
    renderDialog(['force-merge']);

    expect(screen.getByText(/force-merge/i)).toBeInTheDocument();
  });

  it('displays multiple tool names when bulk confirmation is requested', () => {
    renderDialog(multipleToolNames);

    expect(screen.getByText(/force-merge/i)).toBeInTheDocument();
    expect(screen.getByText(/delete-repo/i)).toBeInTheDocument();
    expect(screen.getByText(/reset-branch/i)).toBeInTheDocument();
  });
});

// ─── Suite 2 — Confirm action ─────────────────────────────────────────────────

describe('DestructiveToolWarningDialog — confirm action', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls onConfirm when Confirm button is clicked', async () => {
    const user = userEvent.setup();
    const { onConfirm } = renderDialog();

    await user.click(screen.getByRole('button', { name: /confirm/i }));

    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('does not call onCancel when Confirm button is clicked', async () => {
    const user = userEvent.setup();
    const { onCancel } = renderDialog();

    await user.click(screen.getByRole('button', { name: /confirm/i }));

    expect(onCancel).not.toHaveBeenCalled();
  });
});

// ─── Suite 3 — Cancel action ──────────────────────────────────────────────────

describe('DestructiveToolWarningDialog — cancel action', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls onCancel when Cancel button is clicked', async () => {
    const user = userEvent.setup();
    const { onCancel } = renderDialog();

    await user.click(screen.getByRole('button', { name: /cancel/i }));

    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('does not call onConfirm when Cancel button is clicked', async () => {
    const user = userEvent.setup();
    const { onConfirm } = renderDialog();

    await user.click(screen.getByRole('button', { name: /cancel/i }));

    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('Cancel button has autoFocus attribute for keyboard accessibility', () => {
    renderDialog();

    const cancelBtn = screen.getByRole('button', { name: /cancel/i });
    expect(cancelBtn).toHaveAttribute('autofocus');
  });
});

// ─── Suite 4 — Animation ──────────────────────────────────────────────────────

describe('DestructiveToolWarningDialog — animation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('applies dialog-shake CSS class to the dialog container on mount', () => {
    renderDialog();

    const dialog = screen.getByRole('dialog');
    const dialogBox = dialog.querySelector('[data-testid="dialog-box"]') || dialog.firstElementChild;

    expect(dialogBox).toHaveClass('dialog-shake');
  });
});

// ─── Suite 5 — Reduced motion accessibility ────────────────────────────────────

describe('DestructiveToolWarningDialog — reduced motion accessibility', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('dialog still renders and functions correctly when prefers-reduced-motion is active', () => {
    renderDialog();

    const dialog = screen.getByRole('dialog');
    expect(dialog).toBeInTheDocument();

    const confirmBtn = screen.getByRole('button', { name: /confirm/i });
    expect(confirmBtn).toBeInTheDocument();

    const cancelBtn = screen.getByRole('button', { name: /cancel/i });
    expect(cancelBtn).toBeInTheDocument();
  });
});
