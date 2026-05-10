import React from 'react';
import { Server } from 'lucide-react';

interface McpServerEmptyStateProps {
  onAdd: () => void;
}

const McpServerEmptyState: React.FC<McpServerEmptyStateProps> = ({ onAdd }) => (
  <div className="flex flex-col items-center justify-center gap-4 py-20 text-center">
    <Server size={64} className="text-textMuted" />
    <h2 className="text-xl font-semibold text-text">No MCP Servers yet</h2>
    <p className="text-sm text-textMuted">
      Connect your first MCP server to unlock tool discovery for your agents.
    </p>
    <button
      onClick={onAdd}
      className="px-4 py-2 bg-primary text-white rounded-lg font-medium hover:bg-primaryHover transition-colors"
    >
      Add MCP Server
    </button>
  </div>
);

export default McpServerEmptyState;
