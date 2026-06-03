import React, { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Loader2, AlertTriangle } from 'lucide-react';
import { createSkillFolder } from '../../services/skillFolderService';
import { useSkillFolderForm } from '../../hooks/useSkillFolderForm';
import { FolderPickerInput } from '../../components/cli/FolderPickerInput';

const SkillFolderCreatePage: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();

  const { formState, setFormState, validationErrors, setValidationErrors, clearFieldError, validateForm } = useSkillFolderForm();
  const [isSaving, setIsSaving] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    const errors = validateForm();
    if (Object.keys(errors).length > 0) {
      setValidationErrors(errors);
      return;
    }
    setIsSaving(true);
    setSubmitError(null);
    try {
      await createSkillFolder(workspaceId!, {
        name: formState.name.trim(),
        folderPath: formState.folderPath.trim(),
      });
      navigate(`/workspaces/${workspaceId}/skill-folders`);
    } catch (error: unknown) {
      setSubmitError(error instanceof Error ? error.message : 'Failed to create skill folder. Please try again.');
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancel = () => navigate(`/workspaces/${workspaceId}/skill-folders`);

  return (
    <div className="max-w-2xl mx-auto py-8 px-4">
      <div className="bg-surface border border-border rounded-xl shadow-xl shadow-primary/5 overflow-hidden">
        <div className="px-6 py-4 border-b border-border-elevated">
          <h1 className="text-2xl font-bold bg-gradient-to-r from-text to-textMuted bg-clip-text text-transparent">Register Skill Folder</h1>
        </div>

        <form onSubmit={handleSave} className="p-6 space-y-6">
          {submitError && (
            <div className="flex items-center gap-2 p-3 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm">
              <AlertTriangle className="w-4 h-4 shrink-0" />
              <span>{submitError}</span>
            </div>
          )}

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
                placeholder="e.g. My Skills Library"
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
              {isSaving ? 'Saving…' : 'Register Folder'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default SkillFolderCreatePage;
