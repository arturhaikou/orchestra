import React, { useEffect } from 'react';
import { X, RefreshCw } from 'lucide-react';
import { SyncToolsResult } from '../../types';

interface SyncSummaryBannerProps {
  result: SyncToolsResult;
  onDismiss: () => void;
}

const SyncSummaryBanner: React.FC<SyncSummaryBannerProps> = ({ result, onDismiss }) => {
  useEffect(() => {
    const timer = setTimeout(onDismiss, 8000);
    return () => clearTimeout(timer);
  }, [onDismiss]);

  return (
    <div className="flex items-center justify-between gap-3 px-4 py-2.5 bg-primary/10 border border-primary/20 rounded-lg text-sm text-primary">
      <div className="flex items-center gap-2">
        <RefreshCw className="w-3.5 h-3.5 shrink-0" />
        <span>
          {result.added} added · {result.removed} removed · {result.total} total
        </span>
      </div>
      <button onClick={onDismiss} aria-label="Dismiss sync summary" className="hover:text-text transition-colors shrink-0">
        <X className="w-3.5 h-3.5" />
      </button>
    </div>
  );
};

export default SyncSummaryBanner;
