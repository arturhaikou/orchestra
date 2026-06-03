import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Loader2, AlertTriangle } from 'lucide-react';
import { getSkillFolder, updateSkillFolder } from '../../services/skillFolderService';
import { useSkillFolderForm } from '../../hooks/useSkillFolderForm';
import { FolderPickerInput } from '../../components/cli/FolderPickerInput';
import Toast from '../Toast';

const SkillFolderEditPage: React.FC = () => {
  const { workspaceId, skillFolderId } = useParams<{ workspaceId: string; skillFolderId: string }>();
  const navigate = useNavigate();

  const { formState, setFormState, validationErrors, setValidationErrors, clearFieldError, validateForm } = useSkillFolderForm();
  const [isSaving, setIsSaving] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  useEffect(() => {
    if (!workspaceId || !skillFolderId) return;
    setIsLoading(true);
    getSkillFolder(workspaceId, skillFolderId)
      .then(folder => {
        setFormState({ name: folder.name, folderPath: folder.folderPath });
        setIsLoading(false);
      })
      .catch(() => {
        setLoadError('Failed to load skill folder.');
        setIsLoading(false);
      });
  }, [workspaceId, skillFolderId, setFormState]);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    const errors = validateForm();
    if (Object.keys(errors).length > 0) {
      setValidationErrors(errors);
      return;
    }
    setIsSaving(true);
    setToast(null);
    try {
      await updateSkillFolder(workspaceId!, skillFolderId!, {
        name: formState.name.trim(),
        folderPath: formState.folderPath.trim(),
      });
      setToast({ message: 'Skill folder updated successfully.', type: 'success' });
      navigate(`/workspaces/${workspaceId}/skill-folders`);
    } catch (error: unknown) {
      setToast({ message: error instanceof Error ? error.message : 'Failed to update skill folder.', type: 'error' });
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancel = () => navigate(`/workspaces/${workspaceId}/skill-folders`);

  if (isLoading) {
    return (
      <div className="max-w-2xl mx-auto py-8 px-4 flex items-center justify-center py-20">
        <Loader2 className="w-6 h-6 animate-spin text-primary" />
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="max-w-2xl mx-auto py-8 px-4">
        <div className="flex items-center gap-2 p-4 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm">
          <AlertTriangle className="w-4 h-4 shrink-0" />
          <span>{loadError}</span>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-2xl mx-auto py-8 px-4">
      <div className="bg-surface border border-border rounded-xl shadow-xl shadow-primary/5 overflow-hidden">
        <div className="px-6 py-4 border-b border-border-elevated">
          <h1 className="text-2xl font-bold bg-gradient-to-r from-text to-textMuted bg-clip-text text-transparent">Edit Skill Folder</h1>
        </div>

        <form onSubmit={handleSave} className="p-6 space-y-6">
          <section className="space-y-4">
            <div>
              <label htmlFor="folder-name" className="block text-sm font-medium text-text mb-1">
                Name
              </label>
              <input
                id="folder-name"
                type="text"
                value={formState.name}
                onChange={e => setFormState(prev => ({ ...prev, name: e.target.value }))}
                onFocus={() => clearFieldError('name')}
                maxLength={200}
                className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary"
              />
              {validationErrors.name ? (
                <p className="text-red-400 text-xs mt-1">{validationErrors.name}</p>
              ) : (
                <p className="text-textMuted text-xs mt-1">{formState.name.length}/200</p>
              )}
            </div>

            <div>
              <label htmlFor="folder-path" className="block text-sm font-medium text-text mb-1">
                Folder Path
              </label>
              <FolderPickerInput
                value={formState.folderPath}
                onChange={value => setFormState(prev => ({ ...prev, folderPath: value }))}
                placeholder="e.g. /home/user/skills or C:\skills"
                className="bg-background border border-border rounded-md text-text text-sm font-mono focus:outline-none focus:ring-2 focus:ring-primary px-3 py-2"
              />
              {validationErrors.folderPath ? (
                <p className="text-red-400 text-xs mt-1">{validationErrors.folderPath}</p>
              ) : (
                <p className="text-textMuted text-xs mt-1">Absolute path to a folder on the server containing skill subdirectories.</p>
              )}
            </div>
          </section>

          <div className="flex justify-end gap-3 pt-4 border-t border-border">
            <button
              type="button"
              onClick={handleCancel}
              disabled={isSaving}
              className="px-4 py-2 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSaving}
              className="px-6 py-2 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-medium transition-colors flex items-center gap-2 shadow-lg shadow-primary/20 hover:shadow-[0_0_20px_rgba(99,102,241,0.2)]"
            >
              {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
              {isSaving ? 'Saving…' : 'Save Changes'}
            </button>
          </div>
        </form>
      </div>

      {toast && (
        <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />
      )}
    </div>
  );
};

export default SkillFolderEditPage;
