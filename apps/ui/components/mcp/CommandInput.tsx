import React from 'react';

interface CommandInputProps {
  value: string;
  error?: string;
  isTouched: boolean;
  isDisabled?: boolean;
  onChange: (value: string) => void;
  onBlur: () => void;
}

const CommandInput: React.FC<CommandInputProps> = ({
  value, error, isTouched, isDisabled, onChange, onBlur,
}) => {
  const hasError = isTouched && !!error;
  return (
    <div>
      <label htmlFor="mcp-command" className="block text-sm font-medium text-textMuted mb-1.5">
        Command <span className="text-red-500">*</span>
      </label>
      <input
        id="mcp-command"
        type="text"
        value={value}
        placeholder="e.g., npx or docker"
        disabled={isDisabled}
        aria-label="Command"
        onChange={e => onChange(e.target.value)}
        onBlur={onBlur}
        className={`w-full bg-surfaceHighlight border text-text text-sm font-mono rounded-md px-3 py-2.5
                    focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary
                    transition-colors disabled:opacity-45 disabled:cursor-not-allowed
                    ${hasError ? 'border-red-500' : 'border-border'}`}
      />
      {hasError && <p className="mt-1.5 text-xs text-red-400">{error}</p>}
    </div>
  );
};

export default CommandInput;
