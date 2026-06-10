import React from 'react';

interface ServerIdentitySectionProps {
  serverName: string;
  nameError?: string;
  onChange: (name: string) => void;
  isDisabled?: boolean;
}

const ServerIdentitySection: React.FC<ServerIdentitySectionProps> = ({
  serverName,
  nameError,
  onChange,
  isDisabled,
}) => (
  <div className="px-7 py-6">
    <h2 className="text-xs font-semibold text-textMuted uppercase tracking-wider mb-5">
      Server Identity
    </h2>

    <div>
      <label
        htmlFor="mcp-server-name"
        className="block text-sm font-medium text-textMuted mb-1.5"
      >
        Server Name
        <span className="text-red-500 ml-0.5">*</span>
      </label>
      <input
        id="mcp-server-name"
        type="text"
        value={serverName}
        onChange={(e) => onChange(e.target.value)}
        disabled={isDisabled}
        className="w-full bg-surfaceHighlight border border-border text-text text-sm rounded-md px-3 py-2.5
                   focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary
                   transition-colors disabled:opacity-45 disabled:cursor-not-allowed"
      />
      {nameError && (
        <p className="mt-1.5 text-xs text-red-400 flex items-center gap-1">{nameError}</p>
      )}
    </div>
  </div>
);

export default ServerIdentitySection;
