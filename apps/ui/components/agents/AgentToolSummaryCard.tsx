import React, { useState } from 'react';
import { X } from 'lucide-react';
import { getCategoryIcon } from '../../utils/categoryIconMap';
import { McpServer } from '../../types';
import McpServerStatusBadge from '../mcp/McpServerStatusBadge';

export interface AgentToolSummaryCardProps {
  sourceId: string;
  sourceName: string;
  selectedCount: number;
  totalCount: number;
  connectionStatus?: McpServer['connectionStatus'];
  onRemove: () => void;
  onOpen: () => void;
}

const CountBadge: React.FC<{ selected: number; total: number }> = ({ selected, total }) => (
  <span
    data-testid="selection-count"
    className="text-xs font-semibold px-2 py-0.5 rounded-full bg-primary/10 text-primary border border-primary/20 whitespace-nowrap"
  >
    {selected} / {total}
  </span>
);

const AgentToolSummaryCard: React.FC<AgentToolSummaryCardProps> = ({
  sourceId,
  sourceName,
  selectedCount,
  totalCount,
  connectionStatus,
  onRemove,
  onOpen,
}) => {
  const [removing, setRemoving] = useState(false);
  const IconComponent = getCategoryIcon(sourceId);

  const handleRemove = (e: React.MouseEvent) => {
    e.stopPropagation();
    setRemoving(true);
    onRemove();
  };

  return (
    <div
      data-testid="tool-summary-card"
      onClick={onOpen}
      role="button"
      aria-label={`${sourceName} tools: ${selectedCount} of ${totalCount} selected`}
      className={`flex items-center gap-2 px-3 py-2 rounded-lg border border-border bg-surface
        cursor-pointer hover:border-primary/50 hover:bg-surfaceHighlight
        transition-opacity duration-100 motion-reduce:transition-none active:bg-surfaceHighlight select-none
        ${removing ? 'opacity-0' : 'opacity-100'}`}
    >
      <IconComponent aria-hidden="true" size={18} className="shrink-0 text-textMuted" />
      <span className="text-sm font-medium text-text">{sourceName}</span>
      <CountBadge selected={selectedCount} total={totalCount} />
      {connectionStatus !== undefined && (
        <McpServerStatusBadge status={connectionStatus} />
      )}
      <button
        type="button"
        data-testid="remove-source-button"
        onClick={handleRemove}
        aria-label={`Remove all ${sourceName} tools`}
        className="ml-auto p-0.5 rounded text-textMuted hover:text-text hover:bg-surfaceHighlight transition-colors"
      >
        <X className="w-3.5 h-3.5" />
      </button>
    </div>
  );
};

export default AgentToolSummaryCard;
