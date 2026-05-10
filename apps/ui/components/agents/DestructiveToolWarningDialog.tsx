import React, { FC, useEffect, useRef } from 'react';
import { TriangleAlert } from 'lucide-react';

export interface DestructiveToolWarningDialogProps {
  toolNames: string[];
  onConfirm: () => void;
  onCancel: () => void;
}

const DestructiveToolWarningDialog: FC<DestructiveToolWarningDialogProps> = ({
  toolNames,
  onConfirm,
  onCancel,
}) => {
  const cancelButtonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (cancelButtonRef.current) {
      cancelButtonRef.current.setAttribute('autofocus', '');
      cancelButtonRef.current.focus();
    }
  }, []);

  useEffect(() => {
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onCancel();
      }
    };

    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [onCancel]);

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/70 backdrop-blur-sm">
      <div role="dialog" aria-modal="true">
        <div
          data-testid="dialog-box"
          className="dialog-shake max-w-md rounded-xl bg-surface shadow-2xl border border-border p-6 w-full mx-4"
        >
          <div className="flex items-start gap-4 mb-4">
            <TriangleAlert
              className="h-6 w-6 text-red-500 flex-shrink-0 mt-0.5"
              aria-hidden="true"
            />
            <h2 className="text-lg font-semibold text-text">Enable Destructive Tool?</h2>
          </div>

          <p className="text-sm text-textMuted mb-4">
            This tool can perform irreversible or high-impact actions. Are you sure you want to enable it?
          </p>

          <div className="mb-6">
            {toolNames.length === 1 ? (
              <p className="text-sm font-mono bg-surfaceHighlight px-3 py-2 rounded text-text">
                {toolNames[0]}
              </p>
            ) : (
              <div className="space-y-2">
                {toolNames.map((name) => (
                  <p
                    key={name}
                    className="text-sm font-mono bg-surfaceHighlight px-3 py-2 rounded text-text"
                  >
                    {name}
                  </p>
                ))}
              </div>
            )}
          </div>

          <div className="flex gap-3">
            <button
              ref={cancelButtonRef}
              onClick={onCancel}
              className="flex-1 px-4 py-2 rounded-lg bg-surface border border-border text-text hover:bg-surfaceHighlight transition-colors text-sm font-medium"
            >
              Cancel
            </button>
            <button
              onClick={onConfirm}
              className="flex-1 px-4 py-2 rounded-lg bg-red-600 text-white hover:bg-red-700 transition-colors text-sm font-medium"
            >
              Confirm
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default DestructiveToolWarningDialog;
