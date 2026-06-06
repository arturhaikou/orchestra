import React from 'react';
import { JobStatus } from '../../types';

interface Props { status: JobStatus; }

const config: Record<JobStatus, { label: string; className: string }> = {
  Pending:        { label: 'Pending',        className: 'bg-yellow-500/10 text-yellow-400 border border-yellow-500/20' },
  Running:        { label: 'Running',        className: 'bg-blue-500/10 text-blue-400 border border-blue-500/20' },
  Completed:      { label: 'Completed',      className: 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20' },
  Failed:         { label: 'Failed',         className: 'bg-red-500/10 text-red-400 border border-red-500/20' },
  WaitingForInput: { label: 'Waiting Input',  className: 'bg-orange-500/10 text-orange-400 border border-orange-500/20' },
  Cancelled:       { label: 'Cancelled',       className: 'bg-zinc-500/10 text-zinc-400 border border-zinc-500/20' },
};

const JobStatusBadge: React.FC<Props> = ({ status }) => {
  const entry = config[status];
  if (!entry) return null;
  const { label, className } = entry;
  return (
    <span className={`text-[11px] font-bold px-2 py-0.5 rounded uppercase tracking-wide ${className}`}>
      {status === 'Running' && <span className="inline-block w-1.5 h-1.5 rounded-full bg-blue-400 animate-pulse mr-1.5 align-middle" />}
      {label}
    </span>
  );
};

export default JobStatusBadge;
