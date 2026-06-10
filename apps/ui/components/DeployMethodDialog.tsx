import React, { useEffect, useRef } from 'react';
import { X, Hammer, LayoutGrid, Terminal } from 'lucide-react';

interface DeployMethodDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onSelectScratch: () => void;
  onSelectBuiltIn: () => void;
  onSelectCli: () => void;
}

function trapFocus(e: KeyboardEvent, container: HTMLDivElement | null) {
  const focusableElements = container?.querySelectorAll<HTMLElement>(
    'button, [tabindex]:not([tabindex="-1"])'
  );
  if (!focusableElements || focusableElements.length === 0) return;

  const firstElement = focusableElements[0];
  const lastElement = focusableElements[focusableElements.length - 1];

  if (e.shiftKey && document.activeElement === firstElement) {
    e.preventDefault();
    lastElement.focus();
  } else if (!e.shiftKey && document.activeElement === lastElement) {
    e.preventDefault();
    firstElement.focus();
  }
}

const DeployMethodDialog: React.FC<DeployMethodDialogProps> = ({
  isOpen,
  onClose,
  onSelectScratch,
  onSelectBuiltIn,
  onSelectCli,
}) => {
  const firstOptionRef = useRef<HTMLButtonElement>(null);
  const dialogRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (isOpen) {
      firstOptionRef.current?.focus();
    }
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
        return;
      }
      if (e.key === 'Tab') {
        trapFocus(e, dialogRef.current);
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="deploy-dialog-title"
    >
      <div
        ref={dialogRef}
        className="bg-surface border border-border w-full max-w-md rounded-xl shadow-2xl overflow-hidden animate-scale-in"
      >
        <DialogHeader onClose={onClose} />
        <p className="px-6 text-sm text-textMuted">
          Choose how you'd like to create your agent.
        </p>
        <OptionCards
          firstOptionRef={firstOptionRef}
          onSelectScratch={onSelectScratch}
          onSelectBuiltIn={onSelectBuiltIn}
          onSelectCli={onSelectCli}
        />
        <div className="px-6 pb-6">
          <button
            onClick={onClose}
            className="w-full text-sm text-textMuted hover:text-text py-2 transition-colors"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
};

const DialogHeader: React.FC<{ onClose: () => void }> = ({ onClose }) => (
  <div className="flex items-center justify-between p-6 pb-2">
    <h2 id="deploy-dialog-title" className="text-lg font-semibold text-text">
      Deploy Agent
    </h2>
    <button
      onClick={onClose}
      className="text-textMuted hover:text-text p-1.5 rounded hover:bg-surfaceHighlight transition-colors"
      aria-label="Close"
    >
      <X className="w-4 h-4" />
    </button>
  </div>
);

interface OptionCardsProps {
  firstOptionRef: React.RefObject<HTMLButtonElement | null>;
  onSelectScratch: () => void;
  onSelectBuiltIn: () => void;
  onSelectCli: () => void;
}

const OptionCards: React.FC<OptionCardsProps> = ({
  firstOptionRef,
  onSelectScratch,
  onSelectBuiltIn,
  onSelectCli,
}) => (
  <div className="grid grid-cols-1 gap-4 p-6">
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <button
        ref={firstOptionRef}
        onClick={onSelectScratch}
        className="bg-surface border border-border rounded-lg p-6 hover:border-primary/50 cursor-pointer transition-all focus-visible:ring-2 focus-visible:ring-primary/50 text-center"
        aria-describedby="scratch-description"
      >
        <div className="flex justify-center mb-3">
          <div className="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center">
            <Hammer className="w-5 h-5 text-primary" />
          </div>
        </div>
        <span className="font-semibold text-text block">Create from Scratch</span>
        <span id="scratch-description" className="text-sm text-textMuted mt-1 block">
          Configure a fully custom agent with your own settings.
        </span>
      </button>

      <button
        onClick={onSelectBuiltIn}
        className="bg-surface border border-border rounded-lg p-6 hover:border-primary/50 cursor-pointer transition-all focus-visible:ring-2 focus-visible:ring-primary/50 text-center"
        aria-describedby="builtin-description"
      >
        <div className="flex justify-center mb-3">
          <div className="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center">
            <LayoutGrid className="w-5 h-5 text-primary" />
          </div>
        </div>
        <span className="font-semibold text-text block">Use Built-In Agent</span>
        <span id="builtin-description" className="text-sm text-textMuted mt-1 block">
          Pick from a catalogue of pre-configured agent templates.
        </span>
      </button>
    </div>

    <button
      onClick={onSelectCli}
      className="bg-surface border border-border rounded-lg p-5 hover:border-primary/50 cursor-pointer transition-all focus-visible:ring-2 focus-visible:ring-primary/50 flex items-center gap-4"
      aria-describedby="cli-description"
    >
      <div className="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center flex-shrink-0">
        <Terminal className="w-5 h-5 text-primary" />
      </div>
      <div className="text-left">
        <span className="font-semibold text-text block">CLI-Based Agent</span>
        <span id="cli-description" className="text-sm text-textMuted mt-0.5 block">
          Create an agent powered by a CLI integration (GitHub Copilot, Claude, Gemini) with custom skills.
        </span>
      </div>
    </button>
  </div>
);

export default DeployMethodDialog;
