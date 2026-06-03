import React, { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Loader2, AlertTriangle } from 'lucide-react';
import { createSkill } from '../../services/skillService';
import { useSkillForm } from '../../hooks/useSkillForm';
import MarkdownPreviewToggle from '../agents/MarkdownPreviewToggle';

const SkillCreatePage: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();

  const { formState, setFormState, validationErrors, setValidationErrors, clearFieldError, validateForm } = useSkillForm();
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
      await createSkill(workspaceId!, {
        name: formState.name.trim(),
        description: formState.description.trim(),
        instructions: formState.instructions.trim(),
      });
      navigate(`/workspaces/${workspaceId}/skills`);
    } catch (error: any) {
      setSubmitError(error.message || 'Failed to create skill. Please try again.');
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancel = () => navigate(`/workspaces/${workspaceId}/skills`);

  return (
    <div className="max-w-4xl mx-auto py-8 px-4">
      <div className="bg-surface border border-border rounded-xl shadow-xl shadow-primary/5 overflow-hidden">
        <div className="px-6 py-4 border-b border-border-elevated">
          <h1 className="text-2xl font-bold bg-gradient-to-r from-text to-textMuted bg-clip-text text-transparent">Create Skill</h1>
        </div>

        <form onSubmit={handleSave} className="p-6 space-y-6">
          {submitError && (
            <div className="flex items-center gap-2 p-3 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm">
              <AlertTriangle className="w-4 h-4 shrink-0" />
              <span>{submitError}</span>
            </div>
          )}

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
              className="px-6 py-2 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-medium transition-colors flex items-center gap-2 shadow-lg shadow-primary/20 hover:shadow-[0_0_20px_rgba(99,102,241,0.2)]"
            >
              {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
              {isSaving ? 'Saving…' : 'Create Skill'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default SkillCreatePage;
