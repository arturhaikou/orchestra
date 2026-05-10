import React from 'react';
import { Link } from 'react-router-dom';

interface McpServerBreadcrumbProps {
  workspaceId: string;
}

const McpServerBreadcrumb: React.FC<McpServerBreadcrumbProps> = ({ workspaceId }) => (
  <nav aria-label="Breadcrumb" className="flex items-center gap-1 text-sm mb-6">
    <Link
      to={`/workspaces/${workspaceId}/mcp-servers`}
      className="text-indigo-400 hover:text-indigo-300 hover:underline transition-colors"
    >
      MCP Servers
    </Link>
    <span className="text-zinc-600 mx-1">›</span>
    <span className="text-zinc-400">Add MCP Server</span>
  </nav>
);

export default McpServerBreadcrumb;
