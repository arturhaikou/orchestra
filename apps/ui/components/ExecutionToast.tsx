import React from 'react';
import { X, CheckCircle, XCircle, ExternalLink, ArrowRight } from 'lucide-react';
import { ExecutionToastData } from '../types';
import { isValidReviewUrl } from '../hooks/useExecutionToasts';

interface ExecutionToastProps {
  toast: ExecutionToastData;
  onDismiss: (id: string) => void;
  onViewTicket: (ticketId: string) => void;
}

const ExecutionToast: React.FC<ExecutionToastProps> = ({ toast, onDismiss, onViewTicket }) => {
  const isSuccess = toast.status === 'success';
  const borderColor = isSuccess ? 'border-emerald-500' : 'border-red-500';
  const StatusIcon = isSuccess ? CheckCircle : XCircle;
  const statusLabel = isSuccess ? 'Success' : 'Failed';
  const statusBadgeClasses = isSuccess
    ? 'bg-emerald-500/20 text-emerald-400'
    : 'bg-red-500/20 text-red-400';
  const progressBarColor = isSuccess ? 'bg-emerald-500' : 'bg-red-500';
  const avatarBg = isSuccess ? 'bg-emerald-500/20' : 'bg-red-500/20';
  const avatarText = isSuccess ? 'text-emerald-400' : 'text-red-400';

  const handleOpenReview = () => {
    if (isValidReviewUrl(toast.reviewUrl)) {
      window.open(toast.reviewUrl!, '_blank', 'noopener,noreferrer');
    }
  };

  const handleViewTicket = () => {
    onViewTicket(toast.ticketId);
    onDismiss(toast.id);
  };

  return (
    <div
      role="alert"
      aria-live="polite"
      className={`bg-surface border ${borderColor} rounded-lg shadow-2xl overflow-hidden p-4 space-y-2 animate-scale-in`}
    >
      <div className="flex items-center gap-3">
        <div className={`w-8 h-8 rounded-full ${avatarBg} flex items-center justify-center flex-shrink-0`}>
          <span className={`text-sm font-bold ${avatarText}`}>
            {toast.agentName.charAt(0).toUpperCase()}
          </span>
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-sm font-semibold text-text truncate">{toast.agentName}</span>
            <span className={`text-[10px] font-medium px-2 py-0.5 rounded-full flex items-center gap-1 ${statusBadgeClasses}`}>
              <StatusIcon className="w-3 h-3" />
              <span aria-label={`Execution status: ${statusLabel}`}>{statusLabel}</span>
            </span>
          </div>
        </div>
        <button
          onClick={() => onDismiss(toast.id)}
          className="text-textMuted hover:text-text p-1 rounded hover:bg-surfaceHighlight transition-colors flex-shrink-0"
          aria-label="Dismiss notification"
        >
          <X className="w-4 h-4" />
        </button>
      </div>

      <p className="text-xs text-textMuted truncate">{toast.ticketTitle}</p>

      <div className="flex items-center gap-3">
        <button
          onClick={handleViewTicket}
          className="text-xs text-primary hover:underline flex items-center gap-1"
        >
          <ArrowRight className="w-3 h-3" />
          View Ticket
        </button>
        {toast.reviewUrl && isValidReviewUrl(toast.reviewUrl) && (
          <button
            onClick={handleOpenReview}
            className="text-xs text-primary hover:underline flex items-center gap-1"
          >
            <ExternalLink className="w-3 h-3" />
            Open Review
          </button>
        )}
      </div>

      <div className="h-0.5 w-full bg-border rounded-full overflow-hidden">
        <div
          className={`h-full ${progressBarColor} rounded-full`}
          style={{ animation: 'shrink 10s linear forwards' }}
        />
      </div>
    </div>
  );
};

export default ExecutionToast;
