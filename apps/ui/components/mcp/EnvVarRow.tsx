import React from 'react';
import { X } from 'lucide-react';
import { EnvVar, EnvVarValueEditState } from '../../types';

interface EnvVarRowProps {
  index: number;
  envKey: string;
  envValue: string;
  keyError?: string;
  isKeyTouched: boolean;
  valueEditState: EnvVarValueEditState;
  isDisabled?: boolean;
  onChange: (index: number, patch: Partial<EnvVar>) => void;
  onKeyBlur: (index: number) => void;
  onRemove: (index: number) => void;
  onValueEditStateChange: (index: number, state: EnvVarValueEditState) => void;
}

const EnvVarRow: React.FC<EnvVarRowProps> = ({
  index, envKey, envValue, keyError, isKeyTouched, valueEditState,
  isDisabled, onChange, onKeyBlur, onRemove, onValueEditStateChange,
}) => {
  const hasKeyError = isKeyTouched && !!keyError;
  const isMasked = valueEditState === 'masked';

  const handleValueFocus = () => {
    if (isMasked) onValueEditStateChange(index, 'touched');
  };

  return (
    <div className="flex flex-col gap-0.5">
      <div className="flex items-center gap-2">
        <div className="flex-1 flex flex-col gap-0.5">
          <input
            type="text"
            value={envKey}
            disabled={isDisabled}
            placeholder="KEY"
            aria-label={`Environment variable key ${index}`}
            onChange={e => onChange(index, { key: e.target.value })}
            onBlur={() => onKeyBlur(index)}
            className={`w-full bg-raised border text-text text-sm font-mono rounded-md
                        px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-primary/40
                        focus:border-primary transition-colors disabled:opacity-45
                        disabled:cursor-not-allowed ${hasKeyError ? 'border-red-500' : 'border-border'}`}
          />
          {hasKeyError && <p className="text-xs text-red-400">{keyError}</p>}
        </div>
        <input
          role="textbox"
          type={isMasked ? 'password' : 'text'}
          value={isMasked ? '' : envValue}
          disabled={isDisabled}
          placeholder={isMasked ? '••••••' : 'value'}
          aria-label={`Environment variable value ${index}`}
          onChange={e => onChange(index, { value: e.target.value })}
          onFocus={handleValueFocus}
          className="flex-1 bg-raised border border-border text-text text-sm rounded-md
                     px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-primary/40
                     focus:border-primary transition-colors disabled:opacity-45
                     disabled:cursor-not-allowed"
        />
        <button
          type="button"
          disabled={isDisabled}
          aria-label={`Remove variable ${index}`}
          onClick={() => onRemove(index)}
          className="p-1 text-textMuted hover:text-red-400 transition-colors disabled:opacity-45"
        >
          <X size={14} />
        </button>
      </div>
    </div>
  );
};

export default EnvVarRow;
