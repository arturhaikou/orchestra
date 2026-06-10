import React from 'react';

interface EndpointUrlInputProps {
  value: string;
  error?: string;
  touched: boolean;
  isDisabled?: boolean;
  onChange: (patch: { url: string }) => void;
  onBlur: () => void;
}

const EndpointUrlInput: React.FC<EndpointUrlInputProps> = ({
  value, error, touched, isDisabled, onChange, onBlur,
}) => {
  const showError = touched && !!error;

  return (
    <div>
      <label htmlFor="mcp-endpoint-url" className="block text-sm font-medium text-textMuted mb-1.5">
        Endpoint URL<span className="text-red-500 ml-0.5">*</span>
      </label>
      <input
        id="mcp-endpoint-url"
        type="url"
        value={value}
        maxLength={2048}
        autoComplete="off"
        spellCheck={false}
        disabled={isDisabled}
        placeholder="https://example.com/mcp"
        className={`w-full bg-surfaceHighlight border text-text text-sm rounded-md px-3 py-2.5
                    focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary
                    transition-colors disabled:opacity-45 disabled:cursor-not-allowed
                    ${showError ? 'border-red-400' : 'border-border'}`}
        onChange={e => onChange({ url: e.target.value })}
        onBlur={onBlur}
      />
      {showError && <p className="mt-1 text-xs text-red-500">{error}</p>}
    </div>
  );
};

export default EndpointUrlInput;
