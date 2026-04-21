import React, { useState } from 'react';
import { Workspace } from '../../types';
import { deleteWorkspace } from '../../services/workspaceService';
import DeleteWorkspaceModal from './DeleteWorkspaceModal';

interface WorkspaceModalsProps {
  isDeleteModalOpen: boolean;
  workspaceInAction: Workspace | null;
  onDeleteModalClose: () => void;
  onWorkspaceDeleted: (id: string) => void;
}

const WorkspaceModals: React.FC<WorkspaceModalsProps> = ({
  isDeleteModalOpen,
  workspaceInAction,
  onDeleteModalClose,
  onWorkspaceDeleted,
}) => {
  const [isProcessing, setIsProcessing] = useState(false);

  const handleDeleteWorkspace = async () => {
    if (!workspaceInAction) return;

    setIsProcessing(true);
    try {
      await deleteWorkspace(workspaceInAction.id);
      onWorkspaceDeleted(workspaceInAction.id);
      onDeleteModalClose();
    } catch (error) {
      console.error("Failed to delete workspace", error);
    } finally {
      setIsProcessing(false);
    }
  };

  return (
    <>
      {isDeleteModalOpen && workspaceInAction && (
        <DeleteWorkspaceModal
          isOpen={isDeleteModalOpen}
          onClose={onDeleteModalClose}
          onConfirm={handleDeleteWorkspace}
          workspaceName={workspaceInAction.name}
          isProcessing={isProcessing}
        />
      )}
    </>
  );
};

export default WorkspaceModals;
