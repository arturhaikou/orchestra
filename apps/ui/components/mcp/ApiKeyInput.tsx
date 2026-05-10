import React, { useState } from 'react';
import { Eye, EyeOff } from 'lucide-react';
import { ApiKeyEditState } from '../../types';

interface ApiKeyInputProps {
  value: string;
  error?: string;
  touched: boolean;
  isEditMode: boolean;
  isDisabled?: boolean;
  onChange: (patch: { apiKey: string }) => void;
  onBlur: () => void;
  onEditStateChange?: (state: ApiKeyEditState) => void;
}

const ApiKeyInput: React.FC<ApiKeyInputProps> = ({
  value, error, touched, isEditMode, isDisabled, onChange, onBlur, onEditStateChange,
}) => {
  const [isVisible, setIsVisible]   = useState(false);
  const [editState, setEditState]   = useState<ApiKeyEditState>(
    isEditMode ? 'masked' : 'touched'
  );

  const showError           = touched && !!error;
  const isMaskedPlaceholder = isEditMode && editState === 'masked';
  const placeholder         = isMaskedPlaceholder ? '••••••••' : 'Enter your API key';

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>): void => {
    if (isEditMode && editState === 'masked') {
      setEditState('touched');
      onEditStateChange?.('touched');
    }
    onChange({ apiKey: e.target.value });
  };

  return (
    <div>
      <label htmlFor="mcp-api-key" className="block text-sm font-medium text-textMuted mb-1.5">
        API Key<span className="text-red-500 ml-0.5">*</span>
      </label>
      <div className="relative">
        <input
          id="mcp-api-key"
          type={isVisible ? 'text' : 'password'}
          value={value}
          maxLength={4096}
          disabled={isDisabled}
          placeholder={placeholder}
          className={`w-full bg-raised border text-text text-sm rounded-md px-3 py-2.5 pr-10
                      focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary
                      transition-colors disabled:opacity-45 disabled:cursor-not-allowed
                      ${showError ? 'border-red-400' : 'border-border'}`}
          onChange={handleChange}
          onBlur={onBlur}
        />
        <button
          type="button"
          tabIndex={-1}
          onClick={() => setIsVisible(v => !v)}
          aria-label={isVisible ? 'Hide API key' : 'Show API key'}
          className="absolute right-3 top-1/2 -translate-y-1/2 text-zinc-400 hover:text-zinc-200"
        >
          {isVisible ? <EyeOff size={16} /> : <Eye size={16} />}
        </button>
      </div>
      {showError && <p className="mt-1 text-xs text-red-500">{error}</p>}
    </div>
  );
};

export default ApiKeyInput;
