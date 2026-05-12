import React, { useState, useRef, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { AlertTriangle, Plus, RefreshCw, Terminal } from 'lucide-react';
import { AiCliIntegration } from '../../types';
import { useCliIntegrations } from '../../hooks/useCliIntegrations';
import CliIntegrationCard from '../cli/CliIntegrationCard';
import Toast from '../Toast';

const SKELETON_COUNT = 6;

const CliIntegrationsPage: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();
  const { integrations, isLoading, hasError, retry, deleteIntegration } = useCliIntegrations(workspaceId);

  const [deleteTarget, setDeleteTarget] = useState<AiCliIntegration | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  const deleteIconRefs = useRef<Map<string, HTMLButtonElement>>(new Map());

  const addPath = `/workspaces/${workspaceId}/cli-integrations/new`;

  const handleOpenDeleteModal = useCallback(
    (integration: AiCliIntegration, btnRef: React.RefObject<HTMLButtonElement>) => {
      if (btnRef.current) deleteIconRefs.current.set(integration.id, btnRef.current);
      setDeleteError(null);
      setDeleteTarget(integration);
    },
    [],
  );

  const handleDeleteConfirm = useCallback(async () => {
    if (!deleteTarget) return;
    const { name, id } = deleteTarget;
    setIsDeleting(true);
    setDeleteError(null);
    try {
      await deleteIntegration(id);
      setDeleteTarget(null);
      deleteIconRefs.current.delete(id);
      setToast({ message: `CLI integration '${name}' deleted.`, type: 'success' });
    } catch {
      setDeleteError('Failed to delete. Please try again.');
    } finally {
      setIsDeleting(false);
    }
  }, [deleteTarget, deleteIntegration]);

  const handleCancelDelete = useCallback(() => {
    if (isDeleting) return;
    const triggerId = deleteTarget?.id;
    setDeleteTarget(null);
    setDeleteError(null);
    if (triggerId) {
      deleteIconRefs.current.get(triggerId)?.focus();
      deleteIconRefs.current.delete(triggerId);
    }
  }, [isDeleting, deleteTarget]);

  return (
    <div className="flex flex-col gap-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-text">CLI Integrations</h1>
          <p className="text-sm text-textMuted mt-1">Manage AI CLI connections for your workspace.</p>
        </div>
        <button
          onClick={() => navigate(addPath)}
          className="flex items-center gap-2 px-4 py-2 bg-primary text-white rounded-lg font-medium hover:bg-primaryHover transition-colors text-sm"
        >
          <Plus className="w-4 h-4" />
          Add CLI Connection
        </button>
      </div>

      {hasError && <ErrorBanner onRetry={retry} />}

      {isLoading && (
        <CliCardGrid>
          {Array.from({ length: SKELETON_COUNT }).map((_, i) => (
            <CliCardSkeleton key={i} />
          ))}
        </CliCardGrid>
      )}

      {!isLoading && !hasError && integrations.length === 0 && (
        <EmptyState onAdd={() => navigate(addPath)} />
      )}

      {!isLoading && !hasError && integrations.length > 0 && (
        <CliCardGrid>
          {integrations.map(integration => (
            <CliCardWithDeleteRef
              key={integration.id}
              integration={integration}
              onEdit={() => navigate(`/workspaces/${workspaceId}/cli-integrations/${integration.id}/edit`)}
              onOpenDeleteModal={handleOpenDeleteModal}
            />
          ))}
        </CliCardGrid>
      )}

      {deleteTarget && (
        <DeleteModal
          integration={deleteTarget}
          isDeleting={isDeleting}
          error={deleteError}
          onConfirm={handleDeleteConfirm}
          onCancel={handleCancelDelete}
        />
      )}

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

const CliCardGrid: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
    {children}
  </div>
);

const CliCardSkeleton: React.FC = () => (
  <div className="bg-surface border border-border rounded-[10px] h-[136px] flex flex-col animate-pulse">
    <div className="flex items-start gap-3 px-3.5 pt-3.5 pb-2.5">
      <div className="w-9 h-9 rounded-lg bg-border shrink-0" />
      <div className="flex-1 pt-0.5 space-y-1.5">
        <div className="h-4 bg-border rounded w-3/4" />
        <div className="h-3 bg-border rounded w-1/2" />
      </div>
    </div>
    <div className="flex-1 px-3.5 pb-2.5">
      <div className="h-3 bg-border rounded w-1/3" />
    </div>
    <div className="h-10 bg-border/40 rounded-b-[10px]" />
  </div>
);

interface CliCardWithDeleteRefProps {
  integration: AiCliIntegration;
  onEdit: () => void;
  onOpenDeleteModal: (integration: AiCliIntegration, btnRef: React.RefObject<HTMLButtonElement>) => void;
}

const CliCardWithDeleteRef: React.FC<CliCardWithDeleteRefProps> = ({ integration, onEdit, onOpenDeleteModal }) => {
  const deleteBtnRef = useRef<HTMLButtonElement>(null);
  return (
    <CliIntegrationCard
      integration={integration}
      onEdit={onEdit}
      onDelete={() => onOpenDeleteModal(integration, deleteBtnRef)}
      deleteButtonRef={deleteBtnRef}
    />
  );
};

const ErrorBanner: React.FC<{ onRetry: () => void }> = ({ onRetry }) => (
  <div className="bg-red-500/10 border border-red-500/20 rounded-xl p-4 flex items-center justify-between">
    <div className="flex items-center gap-3 text-red-400">
      <AlertTriangle size={16} />
      <span className="text-sm">Could not load CLI integrations. Please try again.</span>
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

const EmptyState: React.FC<{ onAdd: () => void }> = ({ onAdd }) => (
  <div className="flex flex-col items-center justify-center py-20 text-center gap-4">
    <div className="w-14 h-14 rounded-2xl bg-surfaceHighlight border border-border flex items-center justify-center">
      <Terminal className="w-6 h-6 text-textMuted" />
    </div>
    <div>
      <h3 className="text-base font-semibold text-text">No CLI integrations yet</h3>
      <p className="text-sm text-textMuted mt-1">Connect an AI CLI to start using it in your workspace.</p>
    </div>
    <button
      onClick={onAdd}
      className="flex items-center gap-2 px-4 py-2 bg-primary text-white rounded-lg font-medium hover:bg-primaryHover transition-colors text-sm"
    >
      <Plus className="w-4 h-4" />
      Add CLI Connection
    </button>
  </div>
);

interface DeleteModalProps {
  integration: AiCliIntegration;
  isDeleting: boolean;
  error: string | null;
  onConfirm: () => void;
  onCancel: () => void;
}

const DeleteModal: React.FC<DeleteModalProps> = ({ integration, isDeleting, error, onConfirm, onCancel }) => (
  <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm">
    <div className="bg-surface border border-border w-full max-w-sm rounded-xl shadow-2xl p-6 space-y-4">
      <h2 className="text-base font-semibold text-text">Delete CLI Integration</h2>
      <p className="text-sm text-textMuted">
        Are you sure you want to delete{' '}
        <span className="font-medium text-text">{integration.name}</span>? This action cannot be undone.
      </p>
      {error && (
        <div className="flex items-center gap-2 p-3 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm">
          <AlertTriangle size={14} />
          {error}
        </div>
      )}
      <div className="flex justify-end gap-3 pt-1">
        <button
          onClick={onCancel}
          disabled={isDeleting}
          className="px-4 py-2 text-sm text-textMuted hover:text-text border border-border rounded-lg hover:bg-surfaceHighlight transition-colors disabled:opacity-50"
        >
          Cancel
        </button>
        <button
          onClick={onConfirm}
          disabled={isDeleting}
          className="px-4 py-2 text-sm bg-red-600 text-white rounded-lg hover:bg-red-500 transition-colors disabled:opacity-50"
        >
          {isDeleting ? 'Deleting…' : 'Delete'}
        </button>
      </div>
    </div>
  </div>
);

export default CliIntegrationsPage;
