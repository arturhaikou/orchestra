import React, { useState, useCallback } from 'react';
import { useParams, Link } from 'react-router-dom';
import { AlertTriangle, BookOpen, RefreshCw } from 'lucide-react';
import { Skill } from '../../types';
import { useSkills } from '../../hooks/useSkills';
import SkillCard from '../skills/SkillCard';
import Toast from '../Toast';

const SkillsList: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const { skills, isLoading, hasError, retry, deleteSkillById } = useSkills(workspaceId);

  const [deleteTarget, setDeleteTarget] = useState<Skill | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  const newSkillPath = `/workspaces/${workspaceId}/skills/new`;

  const handleOpenDeleteModal = useCallback((skill: Skill) => {
    setDeleteError(null);
    setDeleteTarget(skill);
  }, []);

  const handleDeleteConfirm = useCallback(async () => {
    if (!deleteTarget || !workspaceId) return;
    setIsDeleting(true);
    setDeleteError(null);
    try {
      await deleteSkillById(deleteTarget.id);
      setDeleteTarget(null);
      setToast({ message: `Skill '${deleteTarget.name}' deleted.`, type: 'success' });
    } catch (err) {
      setDeleteError(err instanceof Error ? err.message : 'Failed to delete skill. Please try again.');
    } finally {
      setIsDeleting(false);
    }
  }, [deleteTarget, workspaceId, deleteSkillById]);

  const handleCancelDelete = useCallback(() => {
    if (isDeleting) return;
    setDeleteTarget(null);
    setDeleteError(null);
  }, [isDeleting]);

  return (
    <div className="flex flex-col gap-6 p-6">
      {/* Page header */}
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-text">Skills</h1>
        <Link
          to={newSkillPath}
          className="px-4 py-2 bg-primary text-white rounded-lg font-medium hover:bg-primaryHover transition-colors text-sm"
        >
          New Skill
        </Link>
      </div>

      {/* Error banner */}
      {hasError && (
        <div className="flex items-center justify-between gap-3 p-4 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm">
          <div className="flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 shrink-0" />
            <span>Failed to load skills.</span>
          </div>
          <button
            onClick={retry}
            className="flex items-center gap-1.5 text-xs font-medium hover:text-red-300 transition-colors"
          >
            <RefreshCw className="w-3.5 h-3.5" />
            Retry
          </button>
        </div>
      )}

      {/* Loading skeleton */}
      {isLoading && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
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
      {!isLoading && !hasError && skills.length === 0 && (
        <div className="flex flex-col items-center justify-center py-20 gap-4 text-textMuted">
          <div className="w-16 h-16 rounded-full bg-primary/10 flex items-center justify-center">
            <BookOpen className="w-7 h-7 text-primary opacity-60" />
          </div>
          <div className="text-center">
            <p className="text-base font-medium text-text">No skills yet</p>
            <p className="text-sm mt-1">Create reusable skills and assign them to your agents.</p>
          </div>
          <Link
            to={newSkillPath}
            className="px-4 py-2 bg-primary text-white rounded-lg font-medium hover:bg-primaryHover transition-colors text-sm"
          >
            Create your first skill
          </Link>
        </div>
      )}

      {/* Skills grid */}
      {!isLoading && !hasError && skills.length > 0 && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {skills.map(skill => (
            <SkillCard
              key={skill.id}
              skill={skill}
              workspaceId={workspaceId!}
              onDelete={handleOpenDeleteModal}
            />
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
                <h3 className="text-base font-semibold text-text">Delete Skill</h3>
                <p className="text-sm text-textMuted">
                  Are you sure you want to delete <span className="font-medium text-text">"{deleteTarget.name}"</span>? This will also remove it from any agents it is assigned to.
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

export default SkillsList;
