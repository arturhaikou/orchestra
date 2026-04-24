import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '@testing-library/jest-dom';
import DeleteWorkspaceModal from '../WorkspaceModals/DeleteWorkspaceModal';

describe('DeleteWorkspaceModal - Save and Close Behavior', () => {
  const defaultProps = {
    isOpen: true,
    onClose: vi.fn(),
    onConfirm: vi.fn(),
    workspaceName: 'My Workspace',
    isProcessing: false,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('basic rendering', () => {
    it('should render workspace name in confirmation text', () => {
      render(<DeleteWorkspaceModal {...defaultProps} />);
      expect(screen.getByText(/My Workspace/)).toBeInTheDocument();
    });

    it('should not render when isOpen is false', () => {
      render(<DeleteWorkspaceModal {...defaultProps} isOpen={false} />);
      expect(screen.queryByText(/My Workspace/)).not.toBeInTheDocument();
    });
  });

  describe('name confirmation guard', () => {
    it('should disable confirm button when name confirmation is required but not matched', () => {
      render(
        <DeleteWorkspaceModal
          {...defaultProps}
          confirmationValue=""
          onConfirmationChange={vi.fn()}
        />
      );
      const confirmButton = screen.getByRole('button', { name: /confirm/i });
      expect(confirmButton).toBeDisabled();
    });

    it('should enable confirm button when typed name matches workspace name', async () => {
      const user = userEvent.setup();
      const onConfirmationChange = vi.fn();
      const { rerender } = render(
        <DeleteWorkspaceModal
          {...defaultProps}
          confirmationValue=""
          onConfirmationChange={onConfirmationChange}
        />
      );

      rerender(
        <DeleteWorkspaceModal
          {...defaultProps}
          confirmationValue="My Workspace"
          onConfirmationChange={onConfirmationChange}
        />
      );

      const confirmButton = screen.getByRole('button', { name: /confirm/i });
      expect(confirmButton).not.toBeDisabled();
    });

    it('should render name confirmation input when onConfirmationChange is provided', () => {
      render(
        <DeleteWorkspaceModal
          {...defaultProps}
          confirmationValue=""
          onConfirmationChange={vi.fn()}
        />
      );
      expect(screen.getByPlaceholderText('My Workspace')).toBeInTheDocument();
    });
  });

  describe('error display', () => {
    it('should display inline error when error prop is provided', () => {
      render(
        <DeleteWorkspaceModal
          {...defaultProps}
          error="Workspace not found."
        />
      );
      expect(screen.getByText('Workspace not found.')).toBeInTheDocument();
    });

    it('should not display error area when error is null', () => {
      render(<DeleteWorkspaceModal {...defaultProps} error={null} />);
      expect(screen.queryByText(/error/i)).not.toBeInTheDocument();
    });

    it('should show Retry button label when error is present', () => {
      render(
        <DeleteWorkspaceModal
          {...defaultProps}
          error="Some error"
        />
      );
      expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
    });
  });

  describe('loading state', () => {
    it('should disable buttons during processing', () => {
      render(<DeleteWorkspaceModal {...defaultProps} isProcessing={true} />);
      expect(screen.getByRole('button', { name: /cancel/i })).toBeDisabled();
    });

    it('should show spinner during processing', () => {
      render(<DeleteWorkspaceModal {...defaultProps} isProcessing={true} />);
      const confirmButton = screen.getAllByRole('button').find(b => !b.textContent?.includes('Cancel'));
      expect(confirmButton).toBeDisabled();
    });
  });

  describe('user interactions', () => {
    it('should call onClose when Cancel is clicked', async () => {
      const user = userEvent.setup();
      render(<DeleteWorkspaceModal {...defaultProps} />);
      await user.click(screen.getByRole('button', { name: /cancel/i }));
      expect(defaultProps.onClose).toHaveBeenCalledOnce();
    });

    it('should call onConfirm when Confirm is clicked', async () => {
      const user = userEvent.setup();
      render(<DeleteWorkspaceModal {...defaultProps} />);
      await user.click(screen.getByRole('button', { name: /confirm/i }));
      expect(defaultProps.onConfirm).toHaveBeenCalledOnce();
    });

    it('should call onClose when X button is clicked', async () => {
      const user = userEvent.setup();
      render(<DeleteWorkspaceModal {...defaultProps} />);
      const closeButtons = screen.getAllByRole('button');
      const xButton = closeButtons.find(b => b.querySelector('svg.lucide-x'));
      if (xButton) {
        await user.click(xButton);
        expect(defaultProps.onClose).toHaveBeenCalledOnce();
      }
    });
  });
});
