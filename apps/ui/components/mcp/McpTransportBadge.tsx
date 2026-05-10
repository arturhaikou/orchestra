import React from 'react';

interface McpTransportBadgeProps {
  transportType: 'HTTP' | 'STDIO';
}

const McpTransportBadge: React.FC<McpTransportBadgeProps> = ({ transportType }) => (
  <span
    aria-label={`Transport: ${transportType === 'HTTP' ? 'HTTP' : 'stdio'}`}
    className={
      transportType === 'HTTP'
        ? 'text-[10px] font-bold px-1.5 py-0.5 rounded border bg-sky-500/10 text-sky-400 border-sky-500/20'
        : 'text-[10px] font-bold px-1.5 py-0.5 rounded border bg-violet-500/10 text-violet-400 border-violet-500/20'
    }
  >
    {transportType === 'HTTP' ? 'HTTP' : 'stdio'}
  </span>
);

export default McpTransportBadge;
