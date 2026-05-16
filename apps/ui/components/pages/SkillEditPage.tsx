import React, { useEffect, useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { Loader2, AlertTriangle } from 'lucide-react';
import { updateSkill } from '../../services/skillService';
import { useLoadSkill } from '../../hooks/useLoadSkill';
import { useSkillForm } from '../../hooks/useSkillForm';
import MarkdownPreviewToggle from '../agents/MarkdownPreviewToggle';
import Toast from '../Toast';

const SkillEditPage: React.FC = () => {
  const { workspaceId, skillId } = useParams<{ workspaceId: string; skillId: string }>();
  const navigate = useNavigate();

  const { skill, isLoading, loadError } = useLoadSkill(workspaceId, skillId);
  const { formState, setFormState, validationErrors, setValidationErrors, clearFieldError, validateForm } = useSkillForm();
  const [isSaving, setIsSaving] = useState(false);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [initialized, setInitialized] = useState(false);

  useEffect(() => {
    if (skill && !initialized) {
      setFormState({
        name: skill.name,
        description: skill.description,
        instructions: skill.instructions,
      });
      setInitialized(true);
    }
  }, [skill, initialized, setFormState]);

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
      await updateSkill(workspaceId!, skillId!, {
        name: formState.name.trim(),
        description: formState.description.trim(),
        instructions: formState.instructions.trim(),
      });
      setToast({ message: 'Skill updated successfully.', type: 'success' });
      navigate(`/workspaces/${workspaceId}/skills`);
    } catch (error: any) {
      setToast({ message: error.message || 'Failed to update skill. Please try again.', type: 'error' });
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancel = () => navigate(`/workspaces/${workspaceId}/skills`);

  if (isLoading) {
    return (
      <div className="max-w-4xl mx-auto py-8 px-4">
        <div className="flex items-center justify-center py-20">
          <Loader2 className="w-8 h-8 animate-spin text-primary" />
        </div>
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="max-w-4xl mx-auto py-8 px-4">
        <div className="flex flex-col items-center justify-center py-20 space-y-4">
          <AlertTriangle className="w-12 h-12 text-yellow-500" />
          <h2 className="text-xl font-bold text-text">Skill Not Found</h2>
          <Link
            to={`/workspaces/${workspaceId}/skills`}
            className="text-primary hover:underline text-sm"
          >
            Return to Skills
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto py-8 px-4">
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}

      <div className="bg-surface border border-border rounded-xl shadow-lg overflow-hidden">
        <div className="px-6 py-4 border-b border-border">
          <h1 className="text-2xl font-bold text-text">Edit Skill</h1>
        </div>

        <form onSubmit={handleSave} className="p-6 space-y-6">
          {/* Identity Section */}
          <section className="space-y-4">
            <h2 className="text-lg font-semibold text-text">Identity</h2>

            <div>
              <label htmlFor="skill-name" className="block text-sm font-medium text-text mb-1">
                Name
              </label>
              <input
                id="skill-name"
                type="text"
                value={formState.name}
                onChange={e => setFormState(prev => ({ ...prev, name: e.target.value }))}
                onFocus={() => clearFieldError('name')}
                maxLength={64}
                className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                placeholder="e.g. Code Review Guidelines"
              />
              {validationErrors.name ? (
                <p className="text-red-400 text-xs mt-1">{validationErrors.name}</p>
              ) : (
                <p className="text-textMuted text-xs mt-1">{formState.name.length}/64</p>
              )}
            </div>

            <div>
              <label htmlFor="skill-description" className="block text-sm font-medium text-text mb-1">
                Description
              </label>
              <textarea
                id="skill-description"
                value={formState.description}
                onChange={e => setFormState(prev => ({ ...prev, description: e.target.value }))}
                onFocus={() => clearFieldError('description')}
                maxLength={1024}
                rows={3}
                className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary resize-y"
                placeholder="A brief description of what this skill does…"
              />
              {validationErrors.description ? (
                <p className="text-red-400 text-xs mt-1">{validationErrors.description}</p>
              ) : (
                <p className="text-textMuted text-xs mt-1">{formState.description.length}/1024</p>
              )}
            </div>
          </section>

          {/* Instructions Section */}
          <section className="space-y-4">
            <h2 className="text-lg font-semibold text-text">Instructions</h2>
            <p className="text-sm text-textMuted">
              Detailed instructions that will be injected into the agent's context when this skill is active.
            </p>
            <div>
              <label htmlFor="skill-instructions" className="block text-sm font-medium text-text mb-1">
                Instructions
              </label>
              <MarkdownPreviewToggle
                id="skill-instructions"
                value={formState.instructions}
                onChange={value => setFormState(prev => ({ ...prev, instructions: value }))}
                onFocus={() => clearFieldError('instructions')}
                rows={10}
                placeholder="Describe the skill's behavior, rules, and guidelines in detail…"
              />
              {validationErrors.instructions ? (
                <p className="text-red-400 text-xs mt-1">{validationErrors.instructions}</p>
              ) : (
                <p className="text-textMuted text-xs mt-1">{formState.instructions.length} characters</p>
              )}
            </div>
          </section>

          {/* Form Actions */}
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
              className="px-6 py-2 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-medium transition-colors flex items-center gap-2 shadow-lg shadow-primary/20"
            >
              {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
              {isSaving ? 'Saving…' : 'Save Changes'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default SkillEditPage;
