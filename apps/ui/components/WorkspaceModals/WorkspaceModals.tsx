import React, { useState } from 'react';
import { Workspace } from '../../types';
import { createWorkspace, updateWorkspace, deleteWorkspace } from '../../services/workspaceService';
import CreateWorkspaceModal from './CreateWorkspaceModal';
import EditWorkspaceModal from './EditWorkspaceModal';
import DeleteWorkspaceModal from './DeleteWorkspaceModal';

interface WorkspaceModalsProps {
  workspaces: Workspace[];
  isCreateModalOpen: boolean;
  isEditModalOpen: boolean;
  isDeleteModalOpen: boolean;
  workspaceInAction: Workspace | null;
  onCreateModalClose: () => void;
  onEditModalClose: () => void;
  onDeleteModalClose: () => void;
  onWorkspaceCreated: (workspace: Workspace) => void;
  onWorkspaceUpdated: (workspace: Workspace) => void;
  onWorkspaceDeleted: (id: string) => void;
}

const WorkspaceModals: React.FC<WorkspaceModalsProps> = ({
  workspaces,
  isCreateModalOpen,
  isEditModalOpen,
  isDeleteModalOpen,
  workspaceInAction,
  onCreateModalClose,
  onEditModalClose,
  onDeleteModalClose,
  onWorkspaceCreated,
  onWorkspaceUpdated,
  onWorkspaceDeleted,
}) => {
  const [isProcessing, setIsProcessing] = useState(false);

  const handleCreateWorkspace = async (
    name: string,
    isAiSummarization: boolean,
    isCustomerSatisfactionAnalysis: boolean,
    aiSummarizationModelId?: string,
    customerSatisfactionAnalysisModelId?: string
  ) => {
    if (!name.trim()) {
      console.error('Workspace name is required');
      return;
    }

    setIsProcessing(true);
    try {
      const newWorkspace = await createWorkspace(
        name,
        isAiSummarization,
        isCustomerSatisfactionAnalysis,
        aiSummarizationModelId,
        customerSatisfactionAnalysisModelId
      );

      // Call parent callback to update app state
      onWorkspaceCreated(newWorkspace);

      // Close the modal
      onCreateModalClose();
    } catch (error) {
      console.error('Failed to create workspace:', error);
      // Note: Error is not surfaced to user (by design per existing pattern)
    } finally {
      setIsProcessing(false);
    }
  };

  const handleUpdateWorkspace = async (
    newName: string,
    aiSummarization: boolean,
    customerSatisfactionAnalysis: boolean,
    aiSummarizationModelId?: string,
    customerSatisfactionAnalysisModelId?: string
  ) => {
    if (!workspaceInAction || !newName.trim()) return;

    setIsProcessing(true);
    try {
      const updated = await updateWorkspace(
        workspaceInAction.id,
        newName,
        aiSummarization,
        customerSatisfactionAnalysis,
        aiSummarizationModelId,
        customerSatisfactionAnalysisModelId
      );
      onWorkspaceUpdated(updated);
      onEditModalClose();
    } catch (error) {
      console.error("Failed to update workspace", error);
    } finally {
      setIsProcessing(false);
    }
  };

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
      {isCreateModalOpen && (
        <CreateWorkspaceModal
          isOpen={isCreateModalOpen}
          onClose={onCreateModalClose}
          onSubmit={handleCreateWorkspace}
          isProcessing={isProcessing}
          hasExistingWorkspaces={workspaces.length > 0}
        />
      )}

      {isEditModalOpen && workspaceInAction && (
        <EditWorkspaceModal
          isOpen={isEditModalOpen}
          onClose={onEditModalClose}
          onSubmit={handleUpdateWorkspace}
          workspace={workspaceInAction}
          isProcessing={isProcessing}
        />
      )}

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
