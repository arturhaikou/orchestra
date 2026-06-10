import React, { useState, useCallback } from 'react';
import { useParams, Link } from 'react-router-dom';
import { AlertTriangle, BookOpen, RefreshCw } from 'lucide-react';
import { Skill, SkillFolder } from '../../types';
import { useSkills } from '../../hooks/useSkills';
import { useSkillFolders } from '../../hooks/useSkillFolders';
import SkillCard from '../skills/SkillCard';
import SkillFolderCard from '../skills/SkillFolderCard';
import Toast from '../Toast';

type SkillOrFolder = 
  | { type: 'skill'; data: Skill }
  | { type: 'folder'; data: SkillFolder };

type DeleteTarget = 
  | { type: 'skill'; data: Skill }
  | { type: 'folder'; data: SkillFolder } 
  | null;

const SkillsAndFoldersListPage: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const { skills, isLoading: isSkillsLoading, hasError: hasSkillsError, retry: retrySkills, deleteSkillById } = useSkills(workspaceId);
  const { skillFolders, isLoading: isFoldersLoading, hasError: hasFoldersError, retry: retryFolders, deleteSkillFolderById } = useSkillFolders(workspaceId);

  const isLoading = isSkillsLoading || isFoldersLoading;
  const hasError = hasSkillsError || hasFoldersError;

  const [deleteTarget, setDeleteTarget] = useState<DeleteTarget>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  const newSkillPath = `/workspaces/${workspaceId}/skills/new`;
  const newFolderPath = `/workspaces/${workspaceId}/skill-folders/new`;

  const handleRetry = useCallback(() => {
    retrySkills();
    retryFolders();
  }, [retrySkills, retryFolders]);

  const handleOpenDeleteModal = useCallback((item: SkillOrFolder) => {
    setDeleteError(null);
    setDeleteTarget(item);
  }, []);

  const handleDeleteConfirm = useCallback(async () => {
    if (!deleteTarget || !workspaceId) return;
    setIsDeleting(true);
    setDeleteError(null);
    try {
      if (deleteTarget.type === 'skill') {
        await deleteSkillById(deleteTarget.data.id);
        setToast({ message: `Skill '${deleteTarget.data.name}' deleted.`, type: 'success' });
      } else {
        await deleteSkillFolderById(deleteTarget.data.id);
        setToast({ message: `Skill folder '${deleteTarget.data.name}' deleted.`, type: 'success' });
      }
      setDeleteTarget(null);
    } catch (err) {
      setDeleteError(err instanceof Error ? err.message : 'Failed to delete. Please try again.');
    } finally {
      setIsDeleting(false);
    }
  }, [deleteTarget, workspaceId, deleteSkillById, deleteSkillFolderById]);

  const handleCancelDelete = useCallback(() => {
    if (isDeleting) return;
    setDeleteTarget(null);
    setDeleteError(null);
  }, [isDeleting]);

  // Combine skills and folders into a single list
  const items: SkillOrFolder[] = [
    ...skills.map(s => ({ type: 'skill' as const, data: s })),
    ...skillFolders.map(f => ({ type: 'folder' as const, data: f })),
  ];

  const isEmpty = skills.length === 0 && skillFolders.length === 0;

  return (
    <div className="flex flex-col gap-6 p-6">
      {/* Page header */}
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-text">Skills</h1>
        <div className="flex gap-2">
          <Link
            to={newFolderPath}
            className="px-4 py-2 bg-primary text-white rounded-lg font-medium hover:bg-primaryHover transition-colors text-sm"
          >
            New Skill Folder
          </Link>
          <Link
            to={newSkillPath}
            className="px-4 py-2 bg-primary text-white rounded-lg font-medium hover:bg-primaryHover transition-colors text-sm"
          >
            New Skill
          </Link>
        </div>
      </div>

      {/* Error banner */}
      {hasError && (
        <div className="flex items-center justify-between gap-3 p-4 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm">
          <div className="flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 shrink-0" />
            <span>Failed to load skills and folders.</span>
          </div>
          <button
            onClick={handleRetry}
            className="flex items-center gap-1.5 text-xs font-medium hover:text-red-300 transition-colors"
          >
            <RefreshCw className="w-3.5 h-3.5" />
            Retry
          </button>
        </div>
      )}

      {/* Loading skeleton */}
      {isLoading && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 items-stretch">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="bg-surface border border-border rounded-xl p-5 h-44 animate-pulse">
              <div className="flex items-start gap-3 mb-3">
                <div className="w-9 h-9 rounded-lg bg-surfaceHighlight flex-shrink-0" />
                <div className="flex-1 space-y-2">
                  <div className="h-3.5 bg-surfaceHighlight rounded w-3/4" />
                  <div className="h-2.5 bg-surfaceHighlight rounded w-1/3" />
                </div>
              </div>
              <div className="space-y-2">
                <div className="h-2.5 bg-surfaceHighlight rounded" />
                <div className="h-2.5 bg-surfaceHighlight rounded w-5/6" />
                <div className="h-2.5 bg-surfaceHighlight rounded w-2/3" />
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Empty state */}
      {!isLoading && !hasError && isEmpty && (
        <div className="flex flex-col items-center justify-center py-20 gap-4 text-textMuted">
          <div className="w-16 h-16 rounded-full bg-primary/10 flex items-center justify-center">
            <BookOpen className="w-7 h-7 text-primary opacity-60" />
          </div>
          <div className="text-center">
            <p className="text-base font-medium text-text">No skills yet</p>
            <p className="text-sm mt-1">Create reusable skills or register folders to extend your agents.</p>
          </div>
          <div className="flex gap-2">
            <Link
              to={newSkillPath}
              className="px-4 py-2 bg-primary text-white rounded-lg font-medium hover:bg-primaryHover transition-colors text-sm"
            >
              Create your first skill
            </Link>
            <Link
              to={newFolderPath}
              className="px-4 py-2 bg-primary text-white rounded-lg font-medium hover:bg-primaryHover transition-colors text-sm"
            >
              Register your first folder
            </Link>
          </div>
        </div>
      )}

      {/* Items grid */}
      {!isLoading && !hasError && items.length > 0 && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {items.map(item => (
            <div key={`${item.type}-${item.data.id}`}>
              {item.type === 'skill' ? (
                <SkillCard
                  skill={item.data as Skill}
                  workspaceId={workspaceId!}
                  onDelete={() => handleOpenDeleteModal({ type: 'skill', data: item.data as Skill })}
                />
              ) : (
                <SkillFolderCard
                  skillFolder={item.data as SkillFolder}
                  workspaceId={workspaceId!}
                  onDelete={() => handleOpenDeleteModal({ type: 'folder', data: item.data as SkillFolder })}
                />
              )}
            </div>
          ))}
        </div>
      )}

      {/* Delete confirmation modal */}
      {deleteTarget && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4"
          role="dialog"
          aria-modal="true"
        >
          <div className="absolute inset-0 bg-black/60 backdrop-blur-sm" />
          <div className="relative z-10 w-full max-w-md bg-surface border border-border rounded-xl shadow-2xl p-6 space-y-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-full bg-red-500/10 flex items-center justify-center flex-shrink-0">
                <AlertTriangle className="w-5 h-5 text-red-400" />
              </div>
              <div>
                <h3 className="text-base font-semibold text-text">
                  {deleteTarget.type === 'skill' ? 'Delete Skill' : 'Delete Skill Folder'}
                </h3>
                <p className="text-sm text-textMuted">
                  Are you sure you want to delete <span className="font-medium text-text">"{deleteTarget.data.name}"</span>?
                  {deleteTarget.type === 'skill' 
                    ? ' This will also remove it from any agents it is assigned to.'
                    : ' This will also unassign it from any agents using it.'}
                </p>
              </div>
            </div>

            {deleteError && (
              <p className="text-sm text-red-400 bg-red-500/10 border border-red-500/20 rounded-lg px-3 py-2">
                {deleteError}
              </p>
            )}

            <div className="flex justify-end gap-3 pt-2">
              <button
                onClick={handleCancelDelete}
                disabled={isDeleting}
                className="px-4 py-2 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={handleDeleteConfirm}
                disabled={isDeleting}
                className="px-4 py-2 bg-red-500 hover:bg-red-600 text-white rounded-md text-sm font-medium transition-colors flex items-center gap-2"
              >
                {isDeleting ? (
                  <>
                    <span className="inline-block w-3.5 h-3.5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                    Deleting…
                  </>
                ) : (
                  'Delete'
                )}
              </button>
            </div>
          </div>
        </div>
      )}

      {toast && (
        <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />
      )}
    </div>
  );
};

export default SkillsAndFoldersListPage;
