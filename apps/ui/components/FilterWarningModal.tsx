import React from 'react';
import { AlertTriangle, Loader2 } from 'lucide-react';

interface FilterWarningModalProps {
  isOpen: boolean;
  providerName: string;
  isProcessing: boolean;
  onCancel: () => void;
  onProceed: () => void;
}

const FilterWarningModal: React.FC<FilterWarningModalProps> = ({
  isOpen,
  providerName,
  isProcessing,
  onCancel,
  onProceed,
}) => {
  const getWarningMessage = (provider: string): string => {
    switch (provider.toLowerCase()) {
      case 'jira':
        return 'The system will show tickets from ALL projects in this Jira instance.';
      case 'confluence':
        return 'The documentation will be gathered from ALL spaces in this Confluence instance.';
      default:
        return `The system will sync content from all available resources in this ${provider} instance.`;
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/90 backdrop-blur-sm animate-fade-in">
      <div className="bg-surface border border-border w-full max-w-sm rounded-xl shadow-2xl p-6 space-y-4 animate-scale-in">
        <div className="flex items-center gap-3 text-amber-500">
          <AlertTriangle className="w-6 h-6 shrink-0" />
          <h3 className="text-lg font-bold text-text">Save without filter?</h3>
        </div>
        <p className="text-sm text-textMuted leading-relaxed">
          No filter query was provided for this integration. {getWarningMessage(providerName)}
        </p>
        <p className="text-sm text-amber-500/80 font-medium">
          Make sure this is the intended behavior.
        </p>
        <div className="flex gap-3 pt-2">
          <button
            onClick={onCancel}
            disabled={isProcessing}
            className="flex-1 px-4 py-2 border border-border rounded-lg text-xs font-bold uppercase tracking-widest text-text hover:bg-surfaceHighlight transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            Update Filter
          </button>
          <button
            onClick={onProceed}
            disabled={isProcessing}
            className="flex-1 px-4 py-2 bg-primary hover:bg-primaryHover text-white rounded-lg text-xs font-bold uppercase tracking-widest shadow-lg shadow-primary/20 active:scale-95 transition-all disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
          >
            {isProcessing ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                <span>Saving...</span>
              </>
            ) : (
              'Proceed'
            )}
          </button>
        </div>
      </div>
    </div>
  );
};

export default FilterWarningModal;
