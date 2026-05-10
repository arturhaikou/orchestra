import React from 'react';
import { AlertTriangle } from 'lucide-react';
import { McpDiscoveryError } from '../../types';

interface ConnectionErrorScreenProps {
  error: McpDiscoveryError;
  onRetry: () => void;
  onBack: () => void;
}

const errorMessage: Record<string, string> = {
  ConnectionFailed: 'Unable to reach the MCP server. Please verify the URL and try again.',
  AuthFailed: 'The provided API key was rejected by the server. Please check your credentials.',
  Timeout: 'The server did not respond within 30 seconds. It may be overloaded or unreachable.',
  ZeroTools: 'The MCP server responded but did not advertise any tools. Check the server configuration.',
};

const ConnectionErrorScreen: React.FC<ConnectionErrorScreenProps> = ({ error, onRetry, onBack }) => {
  const isWarning = error.errorType === 'ZeroTools';
  const bannerClass = isWarning
    ? 'bg-yellow bg-yellow-500/10 border-yellow-500/20 text-yellow-400'
    : 'bg-red-500/10 border-red-500/20 text-red-400';

  return (
    <div data-testid="connection-error" className={`space-y-4 p-4 rounded-lg border ${bannerClass}`}>
      <div className="flex items-start gap-2">
        <AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />
        <p className="text-sm">{errorMessage[error.errorType] ?? error.message}</p>
      </div>
      <div className="flex gap-2">
        <button onClick={onRetry} className="flex-1 bg-primary text-white rounded-lg py-2 text-sm font-medium hover:bg-primary/90 transition-colors">Retry Connection</button>
        <button onClick={onBack} className="px-4 py-2 text-sm border border-current rounded-lg hover:bg-current/10 transition-colors">Back</button>
      </div>
    </div>
  );
};

export default ConnectionErrorScreen;
