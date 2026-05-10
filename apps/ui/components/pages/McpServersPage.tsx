import React, { useState, useRef, useCallback } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { AlertTriangle, RefreshCw } from 'lucide-react';
import { McpServer } from '../../types';
import { useMcpServers } from '../../hooks/useMcpServers';
import McpServerGrid from '../mcp/McpServerGrid';
import McpServerCard from '../mcp/McpServerCard';
import McpServerCardSkeleton from '../mcp/McpServerCardSkeleton';
import McpServerEmptyState from '../mcp/McpServerEmptyState';
import DeleteMcpServerModal from '../mcp/DeleteMcpServerModal';
import Toast from '../Toast';

const SKELETON_COUNT = 6;

const McpServersPage: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();
  const { servers, isLoading, hasError, retry, deleteServer, fetchImpact } = useMcpServers(workspaceId);

  const [deleteTarget, setDeleteTarget] = useState<McpServer | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [affectedAgentCount, setAffectedAgentCount] = useState<number | null>(null);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  const deleteIconRefs = useRef<Map<string, HTMLButtonElement>>(new Map());

  const addServerPath = `/workspaces/${workspaceId}/mcp-servers/new`;

  const handleOpenDeleteModal = useCallback(
    (server: McpServer, btnRef: React.RefObject<HTMLButtonElement>) => {
      if (btnRef.current) deleteIconRefs.current.set(server.id, btnRef.current);
      setDeleteError(null);
      setAffectedAgentCount(null);
      setDeleteTarget(server);
      fetchImpact(server.id)
        .then(count => setAffectedAgentCount(count))
        .catch(() => {});
    },
    [fetchImpact],
  );

  const handleDeleteConfirm = useCallback(async () => {
    if (!deleteTarget) return;

    const { name: serverName, id: serverId } = deleteTarget;

    setIsDeleting(true);
    setDeleteError(null);

    const { success, errorMessage } = await deleteServer(serverId);

    setIsDeleting(false);

    if (success) {
      setDeleteTarget(null);
      deleteIconRefs.current.delete(serverId);
      setToast({ message: `MCP Server '${serverName}' deleted.`, type: 'success' });
    } else {
      setDeleteError(errorMessage);
    }
  }, [deleteTarget, deleteServer]);

  const handleCancelDelete = useCallback(() => {
    if (isDeleting) return;
    const triggerId = deleteTarget?.id;
    setDeleteTarget(null);
    setDeleteError(null);
    setAffectedAgentCount(null);
    if (triggerId) {
      deleteIconRefs.current.get(triggerId)?.focus();
      deleteIconRefs.current.delete(triggerId);
    }
  }, [isDeleting, deleteTarget]);

  return (
    <div className="flex flex-col gap-6 p-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-text">MCP Servers</h1>
        <Link
          to={addServerPath}
          className="px-4 py-2 bg-primary text-white rounded-lg font-medium hover:bg-primaryHover transition-colors text-sm"
        >
          Add MCP Server
        </Link>
      </div>

      {hasError && <ErrorBanner onRetry={retry} />}

      {isLoading && (
        <McpServerGrid>
          {Array.from({ length: SKELETON_COUNT }).map((_, i) => (
            <McpServerCardSkeleton key={i} />
          ))}
        </McpServerGrid>
      )}

      {!isLoading && !hasError && servers.length === 0 && (
        <McpServerEmptyState onAdd={() => navigate(addServerPath)} />
      )}

      {!isLoading && !hasError && servers.length > 0 && (
        <McpServerGrid>
          {servers.map(server => (
            <McpServerCardWithDeleteRef
              key={server.id}
              server={server}
              onEdit={() => navigate(`/workspaces/${workspaceId}/mcp-servers/${server.id}/edit`)}
              onOpenDeleteModal={handleOpenDeleteModal}
            />
          ))}
        </McpServerGrid>
      )}

      <DeleteMcpServerModal
        server={deleteTarget}
        isDeleting={isDeleting}
        error={deleteError}
        onCancel={handleCancelDelete}
        onConfirm={handleDeleteConfirm}
        affectedAgentCount={affectedAgentCount}
      />

      {toast && (
        <Toast
          message={toast.message}
          type={toast.type}
          onClose={() => setToast(null)}
        />
      )}
    </div>
  );
};

interface McpServerCardWithDeleteRefProps {
  server: McpServer;
  onEdit: () => void;
  onOpenDeleteModal: (
    server: McpServer,
    btnRef: React.RefObject<HTMLButtonElement>,
  ) => void;
}

const McpServerCardWithDeleteRef: React.FC<McpServerCardWithDeleteRefProps> = ({
  server,
  onEdit,
  onOpenDeleteModal,
}) => {
  const deleteBtnRef = useRef<HTMLButtonElement>(null);

  return (
    <McpServerCard
      server={server}
      onEdit={onEdit}
      onDelete={() => onOpenDeleteModal(server, deleteBtnRef)}
      deleteButtonRef={deleteBtnRef}
    />
  );
};

const ErrorBanner: React.FC<{ onRetry: () => void }> = ({ onRetry }) => (
  <div className="bg-red-500/10 border border-red-500/20 rounded-xl p-4 flex items-center justify-between">
    <div className="flex items-center gap-3 text-red-400">
      <AlertTriangle size={16} />
      <span className="text-sm">Could not load MCP Servers. Please try again.</span>
    </div>
    <button
      onClick={onRetry}
      className="flex items-center gap-1 text-sm text-red-400 hover:text-red-300 transition-colors"
    >
      <RefreshCw size={14} />
      Retry
    </button>
  </div>
);

export default McpServersPage;
