import React from 'react';
import { McpServerTransportType } from '../../types';

interface TransportTypeSelectorProps {
  value: McpServerTransportType;
  onChange: (transport: McpServerTransportType) => void;
  isDisabled?: boolean;
}

const TRANSPORT_OPTIONS: { value: McpServerTransportType; label: string }[] = [
  { value: 'http', label: 'HTTP' },
  { value: 'stdio', label: 'Stdio' },
];

const TransportTypeSelector: React.FC<TransportTypeSelectorProps> = ({
  value,
  onChange,
  isDisabled,
}) => (
  <div
    role="group"
    aria-label="Transport type"
    className="inline-flex bg-surfaceHighlight border border-border rounded-md p-0.5 gap-0.5 mb-5"
  >
    {TRANSPORT_OPTIONS.map((option) => (
      <button
        key={option.value}
        type="button"
        role="radio"
        aria-checked={value === option.value}
        disabled={isDisabled}
        onClick={() => onChange(option.value)}
        className={[
          'px-5 py-1.5 rounded text-sm font-medium transition-all duration-150',
          'disabled:opacity-45 disabled:cursor-not-allowed',
          value === option.value
            ? 'bg-primary text-white shadow-sm shadow-indigo-500/40'
            : 'text-textMuted hover:text-text hover:bg-hover',
        ].join(' ')}
      >
        {option.label}
      </button>
    ))}
  </div>
);

export default TransportTypeSelector;
