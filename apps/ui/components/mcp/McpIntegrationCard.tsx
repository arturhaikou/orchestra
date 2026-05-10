import React from 'react';
import { RefreshCw, Pencil, Trash2 } from 'lucide-react';
import { McpIntegration } from '../../types';
import McpTransportBadge from './McpTransportBadge';

interface McpIntegrationCardProps {
  integration: McpIntegration;
  onSync: () => void;
  onEdit: () => void;
  onDelete: () => void;
}

const McpIntegrationCard: React.FC<McpIntegrationCardProps> = ({ integration, onSync, onEdit, onDelete }) => (
  <div className="flex items-center justify-between gap-4 p-4 bg-surface border border-border rounded-xl">
    <div className="flex items-center gap-3 min-w-0">
      <div
        data-testid="connected-status"
        className={`w-2.5 h-2.5 rounded-full shrink-0 ${integration.connected ? 'bg-green' : 'bg-textMuted'}`}
      />
      <div className="min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-sm font-semibold text-text truncate">{integration.name}</span>
          <span className="text-[10px] font-bold px-1.5 py-0.5 rounded border bg-indigo-500/10 text-indigo-400 border-indigo-500/20">
            MCP
          </span>
          {integration.mcpTransportType && (
            <McpTransportBadge transportType={integration.mcpTransportType} />
          )}
        </div>
        <div className="text-xs text-textMuted mt-0.5 truncate">
          {integration.mcpTransportType === 'HTTP'
            ? integration.mcpEndpointUrl ?? '—'
            : integration.mcpTransportType === 'STDIO'
            ? integration.mcpCommand ?? '—'
            : `${integration.toolCount ?? 0} tools`}
        </div>
      </div>
    </div>
    <div className="flex items-center gap-1 shrink-0">
      <button
        onClick={onSync}
        className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs text-textMuted hover:text-text border border-border rounded-lg transition-colors"
      >
        <RefreshCw className="w-3 h-3" /> Sync Tools
      </button>
      <button
        onClick={onEdit}
        aria-label="Edit integration"
        className="p-1.5 text-textMuted hover:text-text transition-colors rounded-lg"
      >
        <Pencil className="w-3.5 h-3.5" />
      </button>
      <button
        onClick={onDelete}
        aria-label="Delete integration"
        className="p-1.5 text-textMuted hover:text-red-400 transition-colors rounded-lg"
      >
        <Trash2 className="w-3.5 h-3.5" />
      </button>
    </div>
  </div>
);

export default McpIntegrationCard;
