import React from 'react';
import { AlertTriangle } from 'lucide-react';

interface ModalErrorBannerProps {
  error: string | null;
}

const ModalErrorBanner: React.FC<ModalErrorBannerProps> = ({ error }) => {
  if (!error) return null;

  return (
    <div className="flex items-center gap-2 p-3 bg-red-500/10 border border-red-500/20 rounded-lg">
      <AlertTriangle className="w-4 h-4 text-red-500 shrink-0" />
      <p className="text-xs text-red-500">{error}</p>
    </div>
  );
};

export default ModalErrorBanner;
