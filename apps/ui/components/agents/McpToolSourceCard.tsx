import React from 'react';
import { Server, X } from 'lucide-react';
import { McpServer } from '../../types';
import McpServerStatusBadge from '../mcp/McpServerStatusBadge';

export interface McpToolSourceCardProps {
  serverId: string;
  serverName: string;
  selectedToolNames: string[];
  totalToolCount: number;
  connectionStatus: McpServer['connectionStatus'];
  onEdit: () => void;
  onRemove: () => void;
}

const McpBadge: React.FC = () => (
  <span className="text-xs px-1.5 py-0.5 rounded bg-surfaceHighlight text-textMuted border border-border">
    MCP
  </span>
);

const CountBadge: React.FC<{ selected: number; total: number }> = ({ selected, total }) => (
  <span
    data-testid="mcp-selection-count"
    className="text-xs font-semibold px-2 py-0.5 rounded-full bg-primary/10 text-primary border border-primary/20 whitespace-nowrap"
  >
    {selected} / {total}
  </span>
);

export default function McpToolSourceCard(
  props: McpToolSourceCardProps
): React.ReactElement {
  const { serverId, serverName, selectedToolNames, totalToolCount, connectionStatus, onEdit, onRemove } = props;

  const handleRemove = (e: React.MouseEvent) => {
    e.stopPropagation();
    onRemove();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      onEdit();
    }
  };

  return (
    <div
      data-testid="mcp-tool-source-card"
      role="button"
      tabIndex={0}
      aria-label={`Edit ${serverName} MCP tools: ${selectedToolNames.length} of ${totalToolCount} selected`}
      onClick={onEdit}
      onKeyDown={handleKeyDown}
      className="flex items-center gap-2 px-3 py-2 rounded-lg border border-border bg-surface cursor-pointer hover:border-primary/50 hover:bg-surfaceHighlight transition-opacity duration-100 motion-reduce:transition-none active:bg-surfaceHighlight select-none"
    >
      <Server className="w-5 h-5 text-textMuted shrink-0" />
      <span
        className="text-sm font-medium text-text overflow-hidden text-ellipsis whitespace-nowrap"
        title={serverName}
      >
        {serverName}
      </span>
      <McpBadge />
      <CountBadge selected={selectedToolNames.length} total={totalToolCount} />
      <McpServerStatusBadge status={connectionStatus} />
      <button
        type="button"
        data-testid="remove-mcp-server-button"
        aria-label={`Remove all ${serverName} tools`}
        onClick={handleRemove}
        className="ml-auto p-0.5 rounded text-textMuted hover:text-text hover:bg-surfaceHighlight transition-colors"
      >
        <X className="w-3.5 h-3.5" />
      </button>
    </div>
  );
}
