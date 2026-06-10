import React from 'react';
import { Loader2 } from 'lucide-react';

interface ServerNameInputProps {
  value: string;
  error?: string;
  touched: boolean;
  isChecking: boolean;
  isDisabled?: boolean;
  onChange: (patch: { serverName: string }) => void;
  onBlur: () => void;
  clearNameError: () => void;
}

const ServerNameInput: React.FC<ServerNameInputProps> = ({
  value, error, touched, isChecking, isDisabled, onChange, onBlur, clearNameError,
}) => {
  const showError = touched && !!error;

  return (
    <div>
      <label htmlFor="mcp-server-name" className="block text-sm font-medium text-textMuted mb-1.5">
        Server Name<span className="text-red-500 ml-0.5">*</span>
      </label>
      <div className="relative">
        <input
          id="mcp-server-name"
          type="text"
          value={value}
          maxLength={100}
          autoComplete="off"
          disabled={isDisabled}
          placeholder="e.g., My Figma MCP Server"
          className={`w-full bg-surfaceHighlight border text-text text-sm rounded-md px-3 py-2.5 pr-9
                      focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary
                      transition-colors disabled:opacity-45 disabled:cursor-not-allowed
                      ${showError ? 'border-red-400' : 'border-border'}`}
          onChange={e => { clearNameError(); onChange({ serverName: e.target.value }); }}
          onBlur={onBlur}
        />
        {isChecking && (
          <Loader2
            size={14}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-zinc-400 animate-spin"
          />
        )}
      </div>
      {showError && <p className="mt-1 text-xs text-red-500">{error}</p>}
    </div>
  );
};

export default ServerNameInput;
