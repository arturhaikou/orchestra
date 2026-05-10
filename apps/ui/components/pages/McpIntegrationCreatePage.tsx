import React from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import McpTransportForm from '../mcp/McpTransportForm';

const McpIntegrationCreatePage: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();

  const integrationsPath = `/workspaces/${workspaceId}/integrations`;

  const handleSuccess = () => navigate(integrationsPath);
  const handleCancel = () => navigate(integrationsPath);

  return (
    <div className="max-w-3xl mx-auto py-8 px-4">
      <Link
        to={integrationsPath}
        className="inline-flex items-center gap-1 text-sm text-textMuted hover:text-text transition-colors mb-4"
      >
        <ArrowLeft className="w-4 h-4" /> Back to Integrations
      </Link>

      <div className="bg-surface border border-border rounded-xl shadow-lg overflow-hidden">
        <div className="px-6 py-4 border-b border-border">
          <h1 className="text-2xl font-bold text-text">Add MCP Server</h1>
          <p className="text-sm text-textMuted mt-0.5">
            Connect an MCP-compatible server to your workspace via HTTP or stdio transport.
          </p>
        </div>

        <div className="p-6">
          <McpTransportForm
            workspaceId={workspaceId!}
            onSuccess={handleSuccess}
            onCancel={handleCancel}
          />
        </div>
      </div>
    </div>
  );
};

export default McpIntegrationCreatePage;
