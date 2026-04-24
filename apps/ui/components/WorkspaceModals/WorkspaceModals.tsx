import React, { useCallback } from 'react';
import { Workspace } from '../../types';
import { deleteWorkspace } from '../../services/workspaceService';
import { useModalAction } from '../../hooks/useModalAction';
import DeleteWorkspaceModal from './DeleteWorkspaceModal';

interface WorkspaceModalsProps {
  isDeleteModalOpen: boolean;
  workspaceInAction: Workspace | null;
  onDeleteModalClose: () => void;
  onWorkspaceDeleted: (id: string) => void;
  onToast?: (message: string, type: 'success' | 'error') => void;
}

const WorkspaceModals: React.FC<WorkspaceModalsProps> = ({
  isDeleteModalOpen,
  workspaceInAction,
  onDeleteModalClose,
  onWorkspaceDeleted,
  onToast,
}) => {
  const handleSuccess = useCallback(() => {
    if (!workspaceInAction) return;
    onToast?.(`"${workspaceInAction.name}" has been deleted.`, 'success');
    onWorkspaceDeleted(workspaceInAction.id);
    onDeleteModalClose();
  }, [workspaceInAction, onToast, onWorkspaceDeleted, onDeleteModalClose]);

  const { execute, isLoading: isProcessing, error, resetError } = useModalAction(
    async () => {
      if (!workspaceInAction) return;
      await deleteWorkspace(workspaceInAction.id);
    },
    handleSuccess
  );

  const handleClose = () => {
    resetError();
    onDeleteModalClose();
  };

  return (
    <>
      {isDeleteModalOpen && workspaceInAction && (
        <DeleteWorkspaceModal
          isOpen={isDeleteModalOpen}
          onClose={handleClose}
          onConfirm={execute}
          workspaceName={workspaceInAction.name}
          isProcessing={isProcessing}
          error={error}
        />
      )}
    </>
  );
};

export default WorkspaceModals;
