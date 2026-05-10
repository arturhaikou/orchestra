import React from 'react';
import { X } from 'lucide-react';
import type { SaveMcpServerError } from '../../types';

interface SaveErrorBannerProps {
  error: SaveMcpServerError | null;
  onDismiss: () => void;
}

export const SaveErrorBanner: React.FC<SaveErrorBannerProps> = ({ error, onDismiss }) => {
  if (!error) return null;

  return (
    <div
      role="alert"
      className="flex items-start gap-3 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800"
    >
      <span className="flex-1">{error.message}</span>
      <button
        type="button"
        onClick={onDismiss}
        aria-label="Dismiss error"
        className="shrink-0 rounded p-0.5 hover:bg-red-100 focus:outline-none focus:ring-2 focus:ring-red-400"
      >
        <X className="h-4 w-4" aria-hidden="true" />
      </button>
    </div>
  );
};
