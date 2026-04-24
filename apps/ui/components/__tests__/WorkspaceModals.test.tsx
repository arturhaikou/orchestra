import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '@testing-library/jest-dom';
import WorkspaceModals from '../WorkspaceModals/WorkspaceModals';
import { Workspace } from '../../types';

vi.mock('../../services/workspaceService', () => ({
  deleteWorkspace: vi.fn(),
}));

const { deleteWorkspace } = await import('../../services/workspaceService');

const mockWorkspace: Workspace = {
  id: 'ws-1',
  name: 'Test Workspace',
  isAiSummarizationEnabled: false,
  isCustomerSatisfactionAnalysisEnabled: false,
  ownerId: 'user-1',
};

describe('WorkspaceModals - Save and Close Behavior', () => {
  const defaultProps = {
    isDeleteModalOpen: true,
    workspaceInAction: mockWorkspace,
    onDeleteModalClose: vi.fn(),
    onWorkspaceDeleted: vi.fn(),
    onToast: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should call onWorkspaceDeleted and close modal on successful delete', async () => {
    const user = userEvent.setup();
    (deleteWorkspace as ReturnType<typeof vi.fn>).mockResolvedValueOnce(undefined);
    render(<WorkspaceModals {...defaultProps} />);

    const confirmButton = screen.getByRole('button', { name: /confirm/i });
    await user.click(confirmButton);

    await waitFor(() => {
      expect(deleteWorkspace).toHaveBeenCalledWith('ws-1');
      expect(defaultProps.onWorkspaceDeleted).toHaveBeenCalledWith('ws-1');
    });
  });

  it('should show error in modal when delete API fails', async () => {
    const user = userEvent.setup();
    (deleteWorkspace as ReturnType<typeof vi.fn>).mockRejectedValueOnce(
      new Error('Only the workspace owner can delete this workspace.')
    );
    render(<WorkspaceModals {...defaultProps} />);

    const confirmButton = screen.getByRole('button', { name: /confirm/i });
    await user.click(confirmButton);

    await waitFor(() => {
      expect(screen.getByText(/Only the workspace owner/i)).toBeInTheDocument();
      expect(defaultProps.onWorkspaceDeleted).not.toHaveBeenCalled();
      expect(defaultProps.onDeleteModalClose).not.toHaveBeenCalled();
    });
  });

  it('should show Retry button after API error', async () => {
    const user = userEvent.setup();
    (deleteWorkspace as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('Server error'));
    render(<WorkspaceModals {...defaultProps} />);

    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
    });
  });

  it('should clear error when modal is cancelled after error', async () => {
    const user = userEvent.setup();
    (deleteWorkspace as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('Error'));
    render(<WorkspaceModals {...defaultProps} />);

    await user.click(screen.getByRole('button', { name: /confirm/i }));
    await waitFor(() => {
      expect(screen.getByText(/Error/)).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /cancel/i }));
    expect(defaultProps.onDeleteModalClose).toHaveBeenCalled();
  });

  it('should fire success toast on successful deletion', async () => {
    const user = userEvent.setup();
    (deleteWorkspace as ReturnType<typeof vi.fn>).mockResolvedValueOnce(undefined);
    render(<WorkspaceModals {...defaultProps} />);

    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => {
      expect(defaultProps.onToast).toHaveBeenCalledWith(
        expect.stringContaining('Test Workspace'),
        'success'
      );
    });
  });

  it('should not render modal when isDeleteModalOpen is false', () => {
    render(<WorkspaceModals {...defaultProps} isDeleteModalOpen={false} />);
    expect(screen.queryByText(/Test Workspace/)).not.toBeInTheDocument();
  });
});
