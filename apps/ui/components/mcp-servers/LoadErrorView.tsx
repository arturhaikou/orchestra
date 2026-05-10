import React from 'react';
import type { LoadMcpServerErrorCode } from '../../types';

interface LoadErrorViewProps {
  errorCode: LoadMcpServerErrorCode;
  onRetry: () => void;
  onBack: () => void;
}

export function LoadErrorView({ errorCode, onRetry, onBack }: LoadErrorViewProps) {
  return (
    <div className="flex flex-col items-center justify-center gap-4 py-16 text-center">
      <p className="text-destructive">{resolveMessage(errorCode)}</p>
      {errorCode !== 'FORBIDDEN' && (
        <button onClick={onRetry} className="btn-secondary">Retry</button>
      )}
      <button onClick={onBack} className="btn-link">← Back to MCP Servers</button>
    </div>
  );
}

function resolveMessage(errorCode: LoadMcpServerErrorCode): string {
  if (errorCode === 'NOT_FOUND')
    return 'This MCP server could not be found. It may have been deleted.';
  if (errorCode === 'FORBIDDEN')
    return 'You do not have permission to view this server.';
  return 'The server configuration could not be loaded. Please check your connection.';
}
