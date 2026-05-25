import React from 'react';
import { JobStatus } from '../../types';

interface Props {
  selected?: JobStatus;
  onChange: (status?: JobStatus) => void;
}

const FILTERS: Array<{ label: string; value?: JobStatus }> = [
  { label: 'All' },
  { label: 'Running', value: 'Running' },
  { label: 'Completed', value: 'Completed' },
  { label: 'Failed', value: 'Failed' },
  { label: 'Waiting', value: 'WaitingForInput' },
];

const JobStatusFilter: React.FC<Props> = ({ selected, onChange }) => (
  <div className="flex gap-2">
    {FILTERS.map(f => (
      <button
        key={f.label}
        onClick={() => onChange(f.value)}
        className={`text-xs px-3 py-1.5 rounded-full border transition-colors ${
          selected === f.value
            ? 'bg-primary text-white border-primary'
            : 'border-border text-textMuted hover:border-primary/40 hover:text-text'
        }`}
      >
        {f.label}
      </button>
    ))}
  </div>
);

export default JobStatusFilter;
