import React from 'react';
import { useConnectionStatus } from '../hooks/useConnectionStatus';

interface ConnectionStatusBadgeProps {
  workspaceId: string;
}

const statusConfig = {
  connected: {
    dotColor: 'bg-emerald-500',
    pulse: true,
    label: 'Connected',
    textColor: 'text-emerald-400',
  },
  reconnecting: {
    dotColor: 'bg-orange-500',
    pulse: true,
    label: 'Reconnecting…',
    textColor: 'text-orange-400',
  },
  disconnected: {
    dotColor: 'bg-red-500',
    pulse: false,
    label: 'Disconnected',
    textColor: 'text-red-400',
  },
} as const;

const ConnectionStatusBadge: React.FC<ConnectionStatusBadgeProps> = ({ workspaceId }) => {
  const { status } = useConnectionStatus(workspaceId);
  const config = statusConfig[status];

  return (
    <div className="flex items-center gap-1.5 text-xs" title={`SignalR: ${config.label}`}>
      <span className="relative flex h-2 w-2">
        {config.pulse && (
          <span
            className={`animate-ping absolute inline-flex h-full w-full rounded-full ${config.dotColor} opacity-75`}
          />
        )}
        <span className={`relative inline-flex rounded-full h-2 w-2 ${config.dotColor}`} />
      </span>
      <span className={`hidden sm:inline ${config.textColor}`}>{config.label}</span>
    </div>
  );
};

export default ConnectionStatusBadge;
