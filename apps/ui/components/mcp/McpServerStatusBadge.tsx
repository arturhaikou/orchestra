import React from 'react';
import { McpServerConnectionStatus } from '../../types';

interface McpServerStatusBadgeProps {
  status: McpServerConnectionStatus;
}

const STATUS_CONFIG: Record<McpServerConnectionStatus, {
  label: string;
  dotClass: string;
  pillClass: string;
}> = {
  Connected: {
    label: 'Connected',
    dotClass: 'bg-emerald-500',
    pillClass: 'bg-emerald-500/[0.12] text-emerald-400',
  },
  ConnectionFailed: {
    label: 'Connection Failed',
    dotClass: 'bg-red-500',
    pillClass: 'bg-red-500/[0.12] text-red-400',
  },
  Unverified: {
    label: 'Unverified',
    dotClass: 'bg-zinc-500',
    pillClass: 'bg-zinc-500/[0.18] text-zinc-400',
  },
};

const McpServerStatusBadge: React.FC<McpServerStatusBadgeProps> = ({ status }) => {
  const config = STATUS_CONFIG[status];
  return (
    <span
      aria-label={`Connection status: ${config.label}`}
      className={`inline-flex items-center gap-[5px] px-[9px] py-[2px] rounded-full text-[11.5px] font-medium whitespace-nowrap ${config.pillClass}`}
    >
      <span
        className={`w-1.5 h-1.5 rounded-full shrink-0 ${config.dotClass}`}
        aria-hidden="true"
      />
      {config.label}
    </span>
  );
};

export default McpServerStatusBadge;
