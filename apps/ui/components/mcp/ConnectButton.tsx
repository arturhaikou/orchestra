import React from 'react';
import { CheckCircle2, Loader2 } from 'lucide-react';
import { ConnectStatus } from '../../types';

interface ConnectButtonProps {
  connectStatus: ConnectStatus;
  isFormValid: boolean;
  onClick: () => void;
}

export const ConnectButton: React.FC<ConnectButtonProps> = ({
  connectStatus,
  isFormValid,
  onClick,
}) => {
  const isDisabled =
    connectStatus === 'loading' ||
    connectStatus === 'success' ||
    !isFormValid;

  return (
    <button
      type="button"
      className="w-full flex items-center justify-center gap-2 px-3 py-2 rounded-md text-sm font-medium bg-primary text-white disabled:opacity-40 disabled:cursor-not-allowed hover:bg-primary/90 transition-colors"
      disabled={isDisabled}
      aria-busy={connectStatus === 'loading'}
      onClick={onClick}
    >
      {connectStatus === 'loading' && (
        <Loader2 size={14} className="animate-spin" aria-hidden="true" />
      )}
      {connectStatus === 'success' && (
        <CheckCircle2 size={14} aria-hidden="true" />
      )}
      <span>
        {connectStatus === 'loading'
          ? 'Connecting…'
          : connectStatus === 'success'
            ? 'Connected'
            : 'Connect'}
      </span>
    </button>
  );
};

export default ConnectButton;
