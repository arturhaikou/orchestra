import React from 'react';
import { AlertTriangle } from 'lucide-react';
import { IntegrationStatus } from '../../types';

interface IntegrationWarningBadgeProps {
  integrationStatus: IntegrationStatus | null | undefined;
}

const IntegrationWarningBadge: React.FC<IntegrationWarningBadgeProps> = ({ integrationStatus }) => {
  if (!integrationStatus?.integrationMissing) return null;

  return (
    <span
      role="status"
      aria-label="Warning: required integration missing"
      title={integrationStatus.warningMessage || 'Required integration is missing.'}
      className="text-[10px] bg-orange-500/10 border border-orange-500/20 text-orange-400 px-2 py-0.5 rounded flex items-center gap-1"
    >
      <AlertTriangle className="w-3 h-3" />
      Integration Missing
    </span>
  );
};

export default IntegrationWarningBadge;
