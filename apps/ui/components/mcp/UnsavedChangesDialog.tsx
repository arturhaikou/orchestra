import React from 'react';

interface UnsavedChangesDialogProps {
  isOpen: boolean;
  onStay: () => void;
  onLeave: () => void;
}

const UnsavedChangesDialog: React.FC<UnsavedChangesDialogProps> = ({
  isOpen,
  onStay,
  onLeave,
}) => {
  if (!isOpen) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="unsaved-dialog-title"
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
    >
      <div className="bg-surface border border-border rounded-xl shadow-2xl w-full max-w-md p-6">
        <h2
          id="unsaved-dialog-title"
          className="text-base font-semibold text-text mb-2"
        >
          Unsaved changes
        </h2>
        <p className="text-sm text-textMuted mb-6">
          You have unsaved changes. If you leave this page, your progress will be lost.
        </p>
        <div className="flex items-center justify-end gap-3">
          <button
            type="button"
            onClick={onStay}
            className="px-4 py-2 text-sm font-medium text-textMuted
                       border border-border hover:border-zinc-500 rounded-md
                       transition-[border-color,color] duration-150"
          >
            Stay on page
          </button>
          <button
            type="button"
            onClick={onLeave}
            className="px-4 py-2 text-sm font-semibold rounded-md
                       bg-red-600 hover:bg-red-700 text-white transition-colors duration-150"
          >
            Leave without saving
          </button>
        </div>
      </div>
    </div>
  );
};

export default UnsavedChangesDialog;
