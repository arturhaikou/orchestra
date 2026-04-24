import React from 'react';
import { X, Loader2, AlertTriangle, Trash2 } from 'lucide-react';
import ModalErrorBanner from '../ModalErrorBanner';

interface DeleteWorkspaceModalProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void;
  workspaceName: string;
  isProcessing: boolean;
  error?: string | null;
  confirmationValue?: string;
  onConfirmationChange?: (value: string) => void;
}

const DeleteWorkspaceModal: React.FC<DeleteWorkspaceModalProps> = ({
  isOpen,
  onClose,
  onConfirm,
  workspaceName,
  isProcessing,
  error = null,
  confirmationValue = '',
  onConfirmationChange,
}) => {
  if (!isOpen) return null;

  const isNameConfirmed = !onConfirmationChange || confirmationValue === workspaceName;

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
      <div className="bg-surface border border-border w-full max-w-md rounded-xl shadow-2xl overflow-hidden">
        <div className="px-6 py-4 border-b border-border flex justify-between items-center bg-surfaceHighlight/50">
          <h3 className="text-lg font-bold text-text flex items-center gap-2">
            <Trash2 className="w-4 h-4 text-red-500" /> Delete
          </h3>
          <button onClick={onClose} disabled={isProcessing} className="text-textMuted hover:text-text transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="p-6 space-y-4 text-center">
          <div className="w-16 h-16 bg-red-500/10 rounded-full flex items-center justify-center mx-auto mb-2 text-red-500">
            <AlertTriangle className="w-8 h-8" />
          </div>
          <p className="text-sm text-text">
            Confirm delete <span className="font-bold">"{workspaceName}"</span>?
          </p>

          {onConfirmationChange && (
            <div className="text-left">
              <label className="text-xs text-textMuted block mb-1">
                Type <span className="font-semibold text-text">{workspaceName}</span> to confirm
              </label>
              <input
                type="text"
                value={confirmationValue}
                onChange={(e) => onConfirmationChange(e.target.value)}
                className="w-full px-3 py-2 bg-background border border-border rounded-md text-sm text-text focus:outline-none focus:border-primary"
                placeholder={workspaceName}
                disabled={isProcessing}
              />
            </div>
          )}

          {error && <ModalErrorBanner error={error} />}

          <div className="pt-2 flex gap-3">
            <button 
              type="button" 
              onClick={onClose}
              className="flex-1 px-4 py-2 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-colors"
              disabled={isProcessing}
            >
              Cancel
            </button>
            <button 
              onClick={onConfirm}
              disabled={isProcessing || !isNameConfirmed}
              aria-label={isProcessing ? 'Processing' : error ? 'Retry' : 'Confirm'}
              className="flex-1 px-4 py-2 bg-red-500 hover:bg-red-600 text-white rounded-md text-sm font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2 shadow-lg shadow-red-500/20"
            >
              {isProcessing && <Loader2 className="w-4 h-4 animate-spin" />}
              {error ? 'Retry' : 'Confirm'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default DeleteWorkspaceModal;
