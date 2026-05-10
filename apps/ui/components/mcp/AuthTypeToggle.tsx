import React from 'react';
import { HttpAuthType } from '../../types';

interface AuthTypeToggleProps {
  value: HttpAuthType;
  isDisabled?: boolean;
  onChange: (patch: { authType: HttpAuthType }) => void;
}

const AUTH_OPTIONS: { value: HttpAuthType; label: string }[] = [
  { value: 'none',    label: 'None'    },
  { value: 'api_key', label: 'API Key' },
];

const AuthTypeToggle: React.FC<AuthTypeToggleProps> = ({ value, isDisabled, onChange }) => (
  <div>
    <span className="block text-sm font-medium text-textMuted mb-1.5">Authentication Type</span>
    <div
      role="radiogroup"
      aria-label="Authentication type"
      className="flex rounded-md border border-zinc-700 overflow-hidden w-fit"
    >
      {AUTH_OPTIONS.map(opt => (
        <button
          key={opt.value}
          type="button"
          role="radio"
          aria-checked={value === opt.value}
          disabled={isDisabled}
          onClick={() => onChange({ authType: opt.value })}
          className={`px-4 py-1.5 text-sm font-medium transition-colors
            disabled:opacity-45 disabled:cursor-not-allowed
            ${value === opt.value
              ? 'bg-indigo-600 text-white'
              : 'bg-zinc-800 text-zinc-400 hover:text-zinc-200'}`}
        >
          {opt.label}
        </button>
      ))}
    </div>
  </div>
);

export default AuthTypeToggle;
