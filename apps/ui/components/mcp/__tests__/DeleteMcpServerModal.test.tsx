import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import DeleteMcpServerModal from '../DeleteMcpServerModal';
import { McpServer } from '../../../types';

const makeServer = (overrides: Partial<McpServer> = {}): McpServer => ({
  id: 'srv-1',
  workspaceId: 'ws-1',
  name: 'My Analytics Server',
  connectionStatus: 'Connected',
  transportType: 'HTTP',
  endpointUrl: 'https://mcp.example.com',
  createdAt: '2026-01-01T00:00:00.000Z',
  ...overrides,
});

const defaultProps = {
  server: makeServer(),
  isDeleting: false,
  error: null,
  onCancel: vi.fn(),
  onConfirm: vi.fn(),
  affectedAgentCount: null,
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe('Scenario 1: Modal renders with correct content when open', () => {
  it('Modal_WhenServerProvided_ShowsTitle', () => {
    render(<DeleteMcpServerModal {...defaultProps} />);
    expect(screen.getByText('Delete MCP Server?')).toBeInTheDocument();
  });

  it('Modal_WhenServerProvided_ShowsServerNameInBody', () => {
    render(<DeleteMcpServerModal {...defaultProps} />);
    expect(screen.getByText(/Are you sure you want to delete/i)).toBeInTheDocument();
    expect(screen.getByText('My Analytics Server')).toBeInTheDocument();
  });

  it('Modal_WhenServerProvided_ShowsCancelAndDeleteButtons', () => {
    render(<DeleteMcpServerModal {...defaultProps} />);
    expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /delete/i })).toBeInTheDocument();
  });

  it('Modal_WhenServerNull_RendersNothing', () => {
    const { container } = render(<DeleteMcpServerModal {...defaultProps} server={null} />);
    expect(container.firstChild).toBeNull();
  });

  it('Modal_WhenOpen_HasDialogRole', () => {
    render(<DeleteMcpServerModal {...defaultProps} />);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});

describe('Scenario 2: Cancel button closes modal', () => {
  it('CancelButton_OnClick_CallsOnCancel', async () => {
    const onCancel = vi.fn();
    render(<DeleteMcpServerModal {...defaultProps} onCancel={onCancel} />);

    await userEvent.click(screen.getByRole('button', { name: /cancel/i }));

    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('CancelButton_OnClick_DoesNotCallOnConfirm', async () => {
    const onConfirm = vi.fn();
    render(<DeleteMcpServerModal {...defaultProps} onConfirm={onConfirm} />);

    await userEvent.click(screen.getByRole('button', { name: /cancel/i }));

    expect(onConfirm).not.toHaveBeenCalled();
  });
});

describe('Scenario 3: Escape key closes modal (when not deleting)', () => {
  it('EscapeKey_WhenNotDeleting_CallsOnCancel', () => {
    const onCancel = vi.fn();
    render(<DeleteMcpServerModal {...defaultProps} onCancel={onCancel} />);

    fireEvent.keyDown(document, { key: 'Escape', code: 'Escape' });

    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('EscapeKey_WhenIsDeleting_DoesNotCallOnCancel', () => {
    const onCancel = vi.fn();
    render(<DeleteMcpServerModal {...defaultProps} isDeleting={true} onCancel={onCancel} />);

    fireEvent.keyDown(document, { key: 'Escape', code: 'Escape' });

    expect(onCancel).not.toHaveBeenCalled();
  });
});

describe('Scenario 4: Loading state during deletion', () => {
  it('DeleteButton_WhenIsDeleting_IsDisabled', () => {
    render(<DeleteMcpServerModal {...defaultProps} isDeleting={true} />);
    const deleteBtn = screen.getByRole('button', { name: /deleting/i });
    expect(deleteBtn).toBeDisabled();
  });

  it('CancelButton_WhenIsDeleting_IsDisabled', () => {
    render(<DeleteMcpServerModal {...defaultProps} isDeleting={true} />);
    expect(screen.getByRole('button', { name: /cancel/i })).toBeDisabled();
  });

  it('Modal_WhenIsDeleting_HasAriaBusy', () => {
    render(<DeleteMcpServerModal {...defaultProps} isDeleting={true} />);
    expect(screen.getByRole('dialog')).toHaveAttribute('aria-busy', 'true');
  });

  it('DeleteButton_WhenIsDeleting_ShowsLoadingText', () => {
    render(<DeleteMcpServerModal {...defaultProps} isDeleting={true} />);
    expect(screen.getByRole('button', { name: /deleting/i })).toBeInTheDocument();
  });
});

describe('Scenario 6: Deletion failure shows inline error', () => {
  it('Error_WhenProvided_RendersAlertRole', () => {
    render(
      <DeleteMcpServerModal
        {...defaultProps}
        error="Failed to delete 'My Analytics Server'. Please try again."
      />,
    );
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('Error_WhenProvided_ShowsErrorText', () => {
    render(
      <DeleteMcpServerModal
        {...defaultProps}
        error="Failed to delete 'My Analytics Server'. Please try again."
      />,
    );
    expect(
      screen.getByText(/Failed to delete 'My Analytics Server'/i),
    ).toBeInTheDocument();
  });

  it('DeleteButton_WhenErrorPresent_IsEnabled', () => {
    render(
      <DeleteMcpServerModal
        {...defaultProps}
        error="Failed to delete 'My Analytics Server'. Please try again."
      />,
    );
    expect(screen.getByRole('button', { name: /retry delete/i })).not.toBeDisabled();
  });

  it('Error_WhenNull_DoesNotRenderAlert', () => {
    render(<DeleteMcpServerModal {...defaultProps} error={null} />);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});

describe('Scenario 7: Cancel button receives focus by default', () => {
  it('CancelButton_OnModalOpen_HasAutoFocus', () => {
    render(<DeleteMcpServerModal {...defaultProps} />);
    const cancelBtn = screen.getByRole('button', { name: /cancel/i });
    expect(cancelBtn).toHaveAttribute('autofocus');
  });
});

describe('Scenario 8: Backdrop click closes modal', () => {
  it('Backdrop_OnClick_CallsOnCancel', () => {
    const onCancel = vi.fn();
    const { container } = render(<DeleteMcpServerModal {...defaultProps} onCancel={onCancel} />);

    const backdrop = container.firstChild as HTMLElement;
    fireEvent.click(backdrop);

    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('Backdrop_WhenIsDeleting_DoesNotCallOnCancel', () => {
    const onCancel = vi.fn();
    const { container } = render(
      <DeleteMcpServerModal {...defaultProps} isDeleting={true} onCancel={onCancel} />,
    );

    const backdrop = container.firstChild as HTMLElement;
    fireEvent.click(backdrop);

    expect(onCancel).not.toHaveBeenCalled();
  });
});

describe('Scenario 4: Agent impact warning', () => {
  it('AgentCount_WhenNonZero_ShowsImpactMessage', () => {
    render(<DeleteMcpServerModal {...defaultProps} affectedAgentCount={3} />);
    expect(
      screen.getByText(/3 agent\(s\) will lose access to tools from this server/i),
    ).toBeInTheDocument();
  });

  it('AgentCount_WhenZero_ShowsZeroImpactMessage', () => {
    render(<DeleteMcpServerModal {...defaultProps} affectedAgentCount={0} />);
    expect(
      screen.getByText(/0 agent\(s\) will lose access to tools from this server/i),
    ).toBeInTheDocument();
  });

  it('AgentCount_WhenOne_ShowsSingularCount', () => {
    render(<DeleteMcpServerModal {...defaultProps} affectedAgentCount={1} />);
    expect(
      screen.getByText(/1 agent\(s\) will lose access to tools from this server/i),
    ).toBeInTheDocument();
  });

  it('AgentCount_WhenNull_ShowsLoadingIndicator', () => {
    render(<DeleteMcpServerModal {...defaultProps} affectedAgentCount={null} />);
    expect(
      screen.queryByText(/agent\(s\) will lose access/i),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole('status') || screen.getByLabelText(/loading/i),
    ).toBeInTheDocument();
  });

  it('AgentCount_WhenNonZero_AlsoShowsCannotBeUndone', () => {
    render(<DeleteMcpServerModal {...defaultProps} affectedAgentCount={2} />);
    expect(screen.getByText(/this action cannot be undone/i)).toBeInTheDocument();
  });
});
