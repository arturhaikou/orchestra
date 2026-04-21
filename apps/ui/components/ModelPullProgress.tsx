import React, { useState } from 'react';
import { Loader2, CheckCircle2, XCircle, Star } from 'lucide-react';
import { WorkspaceModel } from '../types';
import { deleteModel, pullModel } from '../services/workspaceService';

interface ModelPullProgressProps {
  /** The workspace this model belongs to. Required for delete and pull API calls. */
  workspaceId: string;
  /** Current model record, including live status and pullProgress driven from SignalR events. */
  model: WorkspaceModel;
  /** The current default model name for this workspace, used to show the "Set as Default" button. */
  currentDefaultModelName: string;
  /**
   * Called when the user clicks "Set as Default".
   * The parent updates its own state and propagates `defaultModelId` upward; no API call is made here.
   */
  onSetDefault: (modelName: string) => void;
  /** Called after a model has been successfully deleted (204 No Content) so the parent removes the row. */
  onDeleted: (modelName: string) => void;
  /**
   * Called when the user clicks "Retry Pull" on a Failed row.
   * The parent adds a fresh Pulling row; the parent is responsible for calling pullModel again.
   */
  onRetryPull: (modelName: string) => void;
  /**
   * Live progress detail text from the most recent ModelPullProgress SignalR event.
   * Shown beneath the progress bar while status is 'Pulling'.
   */
  pullStatusText?: string;
  /**
   * Called immediately when the user initiates a delete, BEFORE the API call is made.
   * The parent uses this to transition the row to `Removing` state optimistically.
   */
  onDeleteInitiated: (modelName: string) => void;
  /**
   * Whether the current authenticated user is the workspace owner.
   * When false or absent, Delete and Set-as-Default controls are hidden.
   */
  isOwner?: boolean;
}

const ModelPullProgress: React.FC<ModelPullProgressProps> = ({
  workspaceId,
  model,
  currentDefaultModelName,
  onSetDefault,
  onDeleted,
  onRetryPull,
  pullStatusText,
  onDeleteInitiated,
  isOwner,
}) => {
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const isDefault = model.modelName === currentDefaultModelName;

  const handleDelete = async () => {
    setIsDeleting(true);
    setDeleteError(null);
    // Immediately notify the parent to transition the row to Removing state (optimistic).
    onDeleteInitiated(model.modelName);
    try {
      await deleteModel(workspaceId, model.modelName);
      onDeleted(model.modelName);
    } catch (err) {
      const message =
        err instanceof Error ? err.message : 'Failed to delete model.';
      // Surface a user-friendly message for the 409 conflict case.
      if (message.includes('409')) {
        setDeleteError('Cannot delete: a pull is currently in progress.');
      } else {
        setDeleteError(message);
      }
    } finally {
      setIsDeleting(false);
    }
  };

  // ----- Pulling -----
  if (model.status === 'Pulling') {
    const progress = model.pullProgress ?? 0;
    return (
      <div className="p-3 bg-background border border-border rounded-md space-y-2">
        <div className="flex items-center justify-between">
          <span className="text-sm font-medium text-text truncate">{model.modelName}</span>
          <span className="text-[10px] font-semibold text-amber-400 uppercase ml-2 shrink-0">Pulling</span>
        </div>
        {/* Animated progress bar */}
        <div className="w-full bg-surfaceHighlight rounded-full h-1.5 overflow-hidden">
          <div
            className="bg-primary h-1.5 rounded-full transition-all duration-300"
            style={{ width: `${progress}%` }}
          />
        </div>
        <div className="flex items-center justify-between">
          <span className="text-[10px] text-textMuted truncate">{pullStatusText ?? 'Waiting…'}</span>
          <span className="text-[10px] text-textMuted ml-2 shrink-0">{progress}%</span>
        </div>
      </div>
    );
  }

  // ----- Available -----
  if (model.status === 'Available') {
    return (
      <div className="p-3 bg-background border border-border rounded-md space-y-2">
        <div className="flex items-center gap-2">
          <CheckCircle2 size={14} className="text-green-400 shrink-0" />
          <span className="text-sm font-medium text-text truncate flex-1">{model.modelName}</span>
          {isDefault && (
            <span className="text-[10px] font-semibold text-primary uppercase">Default</span>
          )}
        </div>
        {isOwner !== false && (
          <div className="flex items-center gap-2">
            {!isDefault && (
              <button
                type="button"
                onClick={() => onSetDefault(model.modelName)}
                className="flex items-center gap-1 px-2 py-1 rounded text-[11px] font-medium text-textMuted border border-border hover:border-primary hover:text-primary transition-colors"
              >
                <Star size={11} />
                Set as Default
              </button>
            )}
            <button
              type="button"
              disabled={isDeleting}
              onClick={handleDelete}
              className="flex items-center gap-1 px-2 py-1 rounded text-[11px] font-medium text-red-400 border border-border hover:border-red-400 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
            >
              {isDeleting ? <Loader2 size={11} className="animate-spin" /> : <XCircle size={11} />}
              {isDeleting ? 'Removing…' : 'Delete'}
            </button>
          </div>
        )}
        {deleteError && (
          <p className="text-[10px] text-red-400">{deleteError}</p>
        )}
      </div>
    );
  }

  // ----- Failed -----
  if (model.status === 'Failed') {
    return (
      <div className="p-3 bg-background border border-red-900/50 rounded-md space-y-2">
        <div className="flex items-center gap-2">
          <XCircle size={14} className="text-red-400 shrink-0" />
          <span className="text-sm font-medium text-text truncate flex-1">{model.modelName}</span>
          <span className="text-[10px] font-semibold text-red-400 uppercase">Failed</span>
        </div>
        {model.errorMessage && (
          <p className="text-[10px] text-red-400 break-all">{model.errorMessage}</p>
        )}
        <button
          type="button"
          onClick={() => onRetryPull(model.modelName)}
          className="flex items-center gap-1 px-2 py-1 rounded text-[11px] font-medium text-amber-400 border border-border hover:border-amber-400 transition-colors"
        >
          Retry Pull
        </button>
      </div>
    );
  }

  // ----- Removing -----
  // model.status === 'Removing'
  return (
    <div className={`p-3 bg-background border border-border rounded-md${deleteError ? '' : ' opacity-60'}`}>
      <div className="flex items-center gap-2">
        <Loader2 size={14} className="text-textMuted animate-spin shrink-0" />
        <span className="text-sm font-medium text-text truncate flex-1">{model.modelName}</span>
        <span className="text-[10px] font-semibold text-textMuted uppercase">Removing</span>
        <button
          type="button"
          disabled
          className="flex items-center gap-1 px-2 py-1 rounded text-[11px] font-medium text-red-400 border border-border opacity-40 cursor-not-allowed"
        >
          <XCircle size={11} />
          Delete
        </button>
      </div>
      {deleteError && (
        <p className="text-[10px] text-red-400 mt-1">{deleteError}</p>
      )}
    </div>
  );
};

export default ModelPullProgress;
